using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Linq;
using System.Reflection;
using SsisToolbox.Excel.Mapper.Attributes;

namespace SsisToolbox.Excel.Mapper
{
    /// <summary>
    /// Parse Excel files as untyped DataSet or typed collections
    /// </summary>
    public class ExcelMapper : IDisposable
    {
        public ExcelMapper(string fileName, string accessDataBaseEngineConnectionString = null)
        {
            _fileName = fileName;
            _accessDataBaseEngineConnectionString = String.IsNullOrEmpty(accessDataBaseEngineConnectionString)
                ? DefaultAccessDataBaseEngineConnectionString
                : accessDataBaseEngineConnectionString;

            DataSetTableName = "table1";
        }

        public const string DefaultAccessDataBaseEngineConnectionString = "Provider=Microsoft.ACE.OLEDB.12.0; Data Source={0}; Extended Properties=Excel 12.0 XML;";

        private readonly string _accessDataBaseEngineConnectionString;

        /// <summary>
        /// Data set table name to export to
        /// </summary>
        public string DataSetTableName { get; set; }

        private readonly string _fileName;

        private OleDbConnection _aceConnection;

        private void OpenConnection()
        {
            if (_aceConnection == null)
            {
                var aceConnectionString = String.Format(_accessDataBaseEngineConnectionString, _fileName);
                _aceConnection = new OleDbConnection(aceConnectionString);
            }
            if (_aceConnection.State != ConnectionState.Open)
            {
                _aceConnection.Open();
            }
        }

        private const string DefaultExcelSheetName = "[sheet1$]";

        /// <summary>
        /// Read excel as data set
        /// </summary>
        /// <param name="excelSheetName">Excel sheet name to read from</param>
        /// <returns>Filled data set or exception</returns>
        public DataSet ReadExcelAsDataSet(string excelSheetName = DefaultExcelSheetName)
        {
            OpenConnection();
            var cmd = new OleDbDataAdapter("select * from " + excelSheetName, _aceConnection);
            var ds = new DataSet();
            cmd.Fill(ds, DataSetTableName);
            return ds;
        }

        /// <summary>
        /// Skip bad rows or throw an error
        /// </summary>
        public bool SkipBadRows = true;

        /// <summary>
        /// Parse typed data from excel using specified mapping metadata
        /// </summary>
        /// <typeparam name="T">Type of target POCO class</typeparam>
        /// <param name="excelSheetName">Excel sheet name to read from</param>
        /// <param name="mappingMetadtaType">Type of POCO mapping metadata contains the name of the columns as [XmlElement] or NULL for default POCO property names</param>
        /// <returns>Mapped list of POCO T instances or exception</returns>
        public List<T> Parse<T>(out string[] warnings, string excelSheetName = DefaultExcelSheetName, Type mappingMetadtaType = null) where T : class
        {
            var dataSet = ReadExcelAsDataSet(excelSheetName);
            return Map<T>(dataSet, mappingMetadtaType, out warnings);
        }

        /// <summary>
        /// Parse typed data from excel using specified mapping metadata
        /// </summary>
        /// <typeparam name="T">Type of target POCO class</typeparam>
        /// <param name="excelSheetName">Excel sheet name to read from</param>
        /// <param name="mappingMetadtaType">Type of POCO mapping metadata contains the name of the columns as [XmlElement] or NULL for default POCO property names</param>
        /// <returns>Mapped list of POCO T instances or exception</returns>
        public List<T> Parse<T>(string excelSheetName = DefaultExcelSheetName, Type mappingMetadtaType = null) where T : class
        {
            string[] warnings;
            return Parse<T>(out warnings, excelSheetName, mappingMetadtaType);
        }

        /// <summary>
        /// Map data from data set to list using specified mapping metadata
        /// </summary>
        /// <typeparam name="T">Type of target POCO class</typeparam>
        /// <param name="dataSet">Source dataset</param>
        /// <param name="mappingMetadtaType">Type of POCO mapping metadata contains the name of the columns as [XmlElement] or NULL for default POCO property names</param>
        /// <returns>Mapped list of POCO T instances or exception</returns>
        public List<T> Map<T>(DataSet dataSet, Type mappingMetadtaType, out string[] warnings) where T : class
        {
            string[] dataSetColumns;
            string[] mappedColumns;
            string[] notMappedColumnsDataSet;
            string[] notMappedColumnsPoco;
            return Map<T>(dataSet, mappingMetadtaType, out dataSetColumns, out mappedColumns, out notMappedColumnsDataSet, out notMappedColumnsPoco, out warnings);
        }

        /// <summary>
        /// Map data from data set to list using specified mapping metadata
        /// </summary>
        /// <typeparam name="T">Type of target POCO class</typeparam>
        /// <param name="dataSet">Source dataset</param>
        /// <param name="mappingMetadtaType">Type of POCO mapping metadata contains the name of the columns as [XmlElement] or NULL for default POCO property names</param>
        /// <param name="dataSetColumnsResult">out - found columns in the excel data set</param>
        /// <param name="mappedColumnsResult">out - columns that can be mapped to </param>
        /// <param name="notMappedColumnsDataSetResult">out - columns in the excel data set the could not be mapped to POCO</param>
        /// <param name="notMappedColumnsPocoResult">out - POCO columns that could not be mapped to excel data set</param>
        /// <returns>Mapped list of POCO T instances or exception</returns>
        public List<T> Map<T>(DataSet dataSet, Type mappingMetadtaType, out string[] dataSetColumnsResult, out string[] mappedColumnsResult, out string[] notMappedColumnsDataSetResult, out string[] notMappedColumnsPocoResult, out string[] warnings) where T : class
        {
            var warningsList = new List<string>();
            var dataSetColumns = new GenericColumnMappingInfo[0];
            var pocoType = typeof(T);

            //get data set columns
            try
            {
                dataSetColumns = dataSet.Tables[DataSetTableName].Columns
                    .Cast<DataColumn>()
                    .Select((p, i) => new GenericColumnMappingInfo(p.ColumnName, i)) //todo: check 0-based
                    .ToArray();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Unable to get column list from excel data set", ex);
            }

            //get mapping schema
            PropertyMappingInfo[] mappingSchema;
            try
            {
                mappingSchema = GetPocoMappingSchema(pocoType, mappingMetadtaType);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Unable to create excel-to-poco mapping schema", ex);
            }

            //properties that could not be mapped
            var dataSetColumnsClosure = dataSetColumns;
            notMappedColumnsPocoResult = mappingSchema.Where(p => dataSetColumnsClosure
                .All(d => p.NotEqualsToMappingInfo(d)))
                .Select(p => p.ToString())
                .ToArray();

            notMappedColumnsDataSetResult = dataSetColumns.Where(p => mappingSchema.All(d => p.NotEqualsToMappingInfo(d)))
                .Select(p => p.ToString())
                .ToArray();

            //properties that can be mapped
            var mappedProperties = new List<PropertyMappingInfo>();

            foreach (var p in mappingSchema)
            {
                var mappedColumn = dataSetColumnsClosure.FirstOrDefault(d => p.EqualsToMappingInfo(d));
                if (mappedColumn != null)
                {
                    mappedProperties.Add(new PropertyMappingInfo(mappedColumn.ColumnName, mappedColumn.ColumnIndex, p.Property));
                }
            }

            var mappedColumns = mappedProperties.Cast<GenericColumnMappingInfo>().ToArray();

            var result = new List<T>();

            var rowNumber = 1;
            foreach (DataRow row in dataSet.Tables[DataSetTableName].Rows)
            {
                try
                {
                    var pocoItem = Activator.CreateInstance<T>();

                    foreach (var mappingInfo in mappedProperties)
                    {
                        object value = null;
                        if (!String.IsNullOrEmpty(mappingInfo.ColumnName))
                        {
                            value = row[mappingInfo.ColumnName];
                        }
                        else if (mappingInfo.ColumnIndex != null)
                        {
                            value = row[mappingInfo.ColumnIndex.Value];
                        }
                        else
                        {
                            throw new InvalidOperationException("Nether index nor name of the colmn cpecified");
                        }

                        try
                        {
                            SetProperty(mappingInfo.Property, pocoItem, value);
                        }
                        catch (Exception ex)
                        {
                            var message = String.Format("Row '{0}': unable to cast value '{1}' of type '{2}' to type '{3}' for the property '{4}'", rowNumber, value, value.GetType().Name, mappingInfo.Property.PropertyType.FullName, mappingInfo.ColumnName);
                            throw new ApplicationException(message, ex);
                        }
                    }

                    result.Add(pocoItem);
                }
                catch (Exception ex)
                {
                    if (SkipBadRows)
                    {
                        warningsList.Add(ex.Message);
                    }
                    else
                    {
                        throw ex;
                    }
                }

                rowNumber++;
            }

            dataSetColumnsResult = dataSetColumns.Select(p => p.ToString()).ToArray();
            mappedColumnsResult = mappedColumns.Select(p => p.ToString()).ToArray();

            warnings = warningsList.ToArray();
            return result;
        }

        private void SetProperty(PropertyInfo property, object instance, object value)
        {
            try
            {
                //cast
                var castedValue = Cast(value, property.PropertyType);
                property.SetValue(instance, castedValue, null);
            }
            catch (Exception)
            {
                //fallback: parse
                var parsedValue = Parse(value, property.PropertyType);
                property.SetValue(instance, parsedValue, null);
            }
        }


        private object Cast(object value, Type targetType)
        {
            //check if nullable
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            var isNullable = underlyingType != null;

            if (value == null || value is DBNull)
            {
                if (isNullable || targetType == typeof(string))
                {
                    return null;
                }
                if (targetType == typeof(object))
                {
                    if (value == null) return null;
                    if (value is DBNull) return DBNull.Value;
                }
                else
                {
                    throw new ApplicationException("A value for non-nullable type expected");
                }
            }
            else if (value is string && underlyingType == typeof(DateTime))
            {
                return DateTime.Parse((string)value);
            }

            return Convert.ChangeType(value, underlyingType ?? targetType);
        }

        private object Parse(object value, Type targetType)
        {
            //check if nullable
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            var isNullable = underlyingType != null;

            var valueStr = value.ToString();
            if (String.IsNullOrEmpty(valueStr))
            {
                if (isNullable)
                {
                    return null;
                }
                else
                {
                    throw new ApplicationException("A value for non-nullable type expected");
                }
            }

            return Convert.ChangeType(valueStr, underlyingType ?? targetType);
        }

        private PropertyMappingInfo[] GetPocoMappingSchema(Type pocoType, Type mappingMetadtaType)
        {
            var pocoProperties = pocoType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance)
            //without metadata property name -> column name
            .ToArray();

            var result = new List<PropertyMappingInfo>();

            if (mappingMetadtaType == null)
            {
                mappingMetadtaType = pocoType;
            }

            if (mappingMetadtaType != null)
            {
                //get metadata properties
                var metadataProperties = mappingMetadtaType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance)
                    .ToArray();

                foreach (var pocoProperty in pocoProperties)
                {
                    //find metadata property for each property
                    var metadataProperty = metadataProperties.FirstOrDefault(p => p.Name == pocoProperty.Name);

                    if (metadataProperty == null)
                    {
                        throw new InvalidOperationException(String.Format("The mapping metadata type '{0}' defines no binding for property '{1}' of the POCO class '{2}'", mappingMetadtaType.Name, pocoProperty.Name, pocoType.Name));
                    }

                    //get xml elements attributes of the metadata property
                    var attribute = metadataProperty
                        .GetCustomAttributes(true)
                        .Where(p => p is MapExcelAttribute)
                        .Cast<MapExcelAttribute>()
                        .FirstOrDefault();

                    if (attribute != null)
                    {
                        //change the column name for the mapping
                        result.Add(new PropertyMappingInfo(attribute.ColumnName, attribute.ColumnIndex, pocoProperty));
                    }
                }
            }

            if (!result.Any())
            {
                throw new InvalidOperationException(String.Format("The mapping metadata type '{0}' defines no [MapExcelAttribute] mapping to setup property binding of the POCO class '{1}'", mappingMetadtaType.Name, pocoType.Name));
            }

            return result.ToArray();
        }

        public void Dispose()
        {
            if (_aceConnection != null)
            {
                _aceConnection.Dispose();
                _aceConnection = null;
            }
        }

        #region protected stuctures

        /// <summary>
        /// Column mapping info interface
        /// </summary>
        protected interface IColumnMappingInfo
        {
            string ColumnName { get; }
            int? ColumnIndex { get; }
        }

        /// <summary>
        /// 
        /// </summary>
        protected class GenericColumnMappingInfo : IColumnMappingInfo
        {
            public GenericColumnMappingInfo()
            {
            }

            public GenericColumnMappingInfo(string columnName, int? columnIndex)
            {
                ColumnName = columnName;
                ColumnIndex = columnIndex;
            }

            public string ColumnName { get; set; }
            public int? ColumnIndex { get; set; }

            public void CopyGenericColumnMappingInfo(IColumnMappingInfo src)
            {
                ColumnName = src.ColumnName;
                ColumnIndex = src.ColumnIndex;
            }

            public bool EqualsToMappingInfo(IColumnMappingInfo src)
            {
                //matched by index when defined
                return (ColumnIndex != null && src.ColumnIndex != null && ColumnIndex == src.ColumnIndex)
                    //or matched by column name when index not defined
                    || (!String.IsNullOrEmpty(ColumnName) && !String.IsNullOrEmpty(src.ColumnName) && String.Compare(ColumnName, src.ColumnName) == 0);
            }

            public bool NotEqualsToMappingInfo(IColumnMappingInfo src)
            {
                //not matched by index when defined
                return (ColumnIndex == null || src.ColumnIndex == null || ColumnIndex != src.ColumnIndex)
                                        //and not matched by column name when index not defined
                                        && (String.IsNullOrEmpty(ColumnName) || String.IsNullOrEmpty(src.ColumnName) || String.Compare(ColumnName, src.ColumnName) != 0);
            }

            public override string ToString()
            {
                if (!String.IsNullOrEmpty(ColumnName))
                {
                    return ColumnName.ToString();
                }
                if (ColumnIndex != null)
                {
                    return ColumnIndex.Value.ToString();
                }
                return "";
            }
        }

        protected class PropertyMappingInfo : GenericColumnMappingInfo
        {
            public PropertyMappingInfo(string columnName, int? columnIndex, PropertyInfo property) : base(columnName, columnIndex)
            {
                Property = property;
            }
            public PropertyInfo Property { get; set; }
        }

        #endregion 

    }//ExcelMapper
}