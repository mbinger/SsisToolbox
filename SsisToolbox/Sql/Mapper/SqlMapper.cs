using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using SsisToolbox.Reliability;
using SsisToolbox.Interface;
using SsisToolbox.Sql.Mapper.Attributes;

namespace SsisToolbox.Sql.Mapper
{
    /// <summary>
    /// Bulk insert typed lists to SQL table
    /// </summary>
    public class SqlMapper : IDisposable
    {
        public SqlMapper(string connectionString)
        {
            _connectionString = connectionString;
            _circuitBreaker = new CircuitBreakerFactory().Create();
        }

        /// <summary>
        /// Bulk insert typed collection to the target SQL table
        /// </summary>
        /// <typeparam name="T">Type of the POCO item</typeparam>
        /// <param name="tableName">Target table</param>
        /// <param name="items">Typed source collection</param>
        public MappingResult BulkInsert<T>(string tableName, IEnumerable<T> items) where T : class
        {
            try
            {
                //get SQL schema
                var sqlSchema = GetSqlTableSchema(tableName);

                //get POCO schema
                var pocoSchema = GetPocoSchema(typeof(T), null); //the T POCO class has the metadata by itself

                //create mapping
                var mapping = CreateMapping(tableName, sqlSchema, pocoSchema);

                //map items to the SQL table
                var result = Map(items, mapping);

                //insert data to the SQL server
                BulkInsertData(mapping.SqlDataTable, mapping.SqlDataTable.TableName, mapping.Map.Select(p => p.SqlColumnIndex).ToArray());

                //group warnings
                result.Warnings = result.Warnings.Distinct().ToList();
                return result;
            }
            catch (ApplicationException ex)
            {
                throw new ApplicationException(String.Format("Unable to insert data into destination table '{0}'", tableName), ex);
            }
        }

        public class MappingResult
        {
            /// <summary>
            /// Number of inserted rows
            /// </summary>
            public int Count { get; set; }

            /// <summary>
            /// Warnings
            /// </summary>
            public List<string> Warnings { get; set; }
        }

        /// <summary>
        /// Map POCO data items to prepared data table
        /// </summary>
        /// <typeparam name="T">Type of the POCO item</typeparam>
        /// <param name="schema">Table schema</param>
        /// <param name="dataTable">Empty prepared data table</param>
        /// <param name="items">POCO items</param>
        protected MappingResult Map<T>(IEnumerable<T> items, Mapping mapping) where T : class
        {
            var result = new MappingResult
            {
                Count = 0,
                Warnings = new List<string>()
            };
            var row = 1;
            foreach (var item in items)
            {
                try
                {
                    var values = new object[mapping.Map.Length];

                    //go thru all mapped columns
                    for (var i = 0; i < values.Length; i++)
                    {
                        var sqlColumnIndex = mapping.Map[i].SqlColumnIndex;
                        var pocoPropertyIndex = mapping.Map[i].PocoPropertyIndex;

                        var sqlColumnInfo = mapping.SqlSchema[sqlColumnIndex];
                        var pocoPropertyInfo = mapping.PocoSchema[pocoPropertyIndex];

                        try
                        {
                            //skip identity autoincrement column 
                            if (sqlColumnInfo.IsIdentity && sqlColumnInfo.IsAutoIncrement)
                            {
                                continue;
                            }

                            //get value of mapped property
                            object value = pocoPropertyInfo.Property.GetValue(item, null);

                            //cast values
                            var castedValue = Cast(value, sqlColumnInfo);
                            values[i] = castedValue;
                        }
                        catch (Exception ex)
                        {
                            throw new ApplicationException(String.Format("Row {0}. Unable to set value for column Nr {1} '{2}'\n{1}", row, i, sqlColumnInfo.ColumnName, ex.Message), ex);
                        }
                    }

                    mapping.SqlDataTable.Rows.Add(values);
                    result.Count++;
                }
                catch (Exception ex)
                {
                    var message = String.Format("Unable to map {0}-th row of the source data table\n{1}", row + 1, ex.Message);
                    if (SkipBadRows)
                    {
                        result.Warnings.Add(message);
                    }
                    else
                    {
                        throw new ApplicationException(message, ex);
                    }
                }

                row++;
            }
            return result;
        }

        private object Cast(object value, SqlColumnInformation sqlColumnInfo)
        {
            if (value == null || value is DBNull)
            {
                if (sqlColumnInfo.AllowDbNull)
                {
                    return DBNull.Value;
                }
                else
                {
                    throw new ApplicationException(String.Format("The column '{0}' does not support NULL values", sqlColumnInfo.ColumnName));
                }
            }
            else if (value is string && sqlColumnInfo.DataType == typeof(DateTime))
            {
                return DateTime.Parse((string)value);
            }

            return Convert.ChangeType(value, sqlColumnInfo.DataType);
        }

        /// <summary>
        /// Get POCO property schema
        /// </summary>
        /// <param name="pocoType">POCO class type</param>
        /// <param name="mappingMetadtaType">Metadata class type or null when the POCO class type has the metadata by itself</param>
        /// <returns></returns>
        private PropertyMappingInfo[] GetPocoSchema(Type pocoType, Type mappingMetadtaType = null)
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
                        .Where(p => p is MapSqlTableAttribute)
                        .Cast<MapSqlTableAttribute>()
                        .FirstOrDefault();

                    if (attribute != null)
                    {
                        string columnName = attribute.ColumnName;
                        int? columnIndex = attribute.ColumnIndex;

                        if (String.IsNullOrEmpty(columnName) && columnIndex == null)
                        {
                            //use default property name
                            columnName = pocoProperty.Name;
                        }

                        //change the column name for the mapping
                        result.Add(new PropertyMappingInfo(columnName, columnIndex, pocoProperty));
                    }
                }
            }

            if (!result.Any())
            {
                throw new InvalidOperationException(String.Format("The mapping metadata type '{0}' defines no [MapExcelAttribute] mapping to setup property binding of the POCO class '{1}'", mappingMetadtaType.Name, pocoType.Name));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Get table schema
        /// </summary>
        /// <param name="tableName">table name with schema</param>
        /// <returns>table schema information</returns>
        protected SqlColumnInformation[] GetSqlTableSchema(string tableName)
        {
            var schema = _circuitBreaker.Action(() =>
            {
                OpenConnection();
                var cmd = new SqlCommand("select top 1 * from " + tableName, _connection);
                using (var reader = cmd.ExecuteReader())
                {
                    return reader.GetSchemaTable();
                }
            });

            var result = new List<SqlColumnInformation>();
            foreach (DataRow row in schema.Rows)
            {
                var columnOrdinal = (int)row["ColumnOrdinal"];
                var columnName = (string)row["ColumnName"];
                var dataType = (Type)row["DataType"];
                var allowDbNull = (bool)row["AllowDbNull"];
                var isIdentity = (bool)row["IsIdentity"];
                var isAutoIncrement = (bool)row["IsAutoIncrement"];

                result.Add(new SqlColumnInformation
                {
                    ColumnOrdinal = columnOrdinal,
                    ColumnName = columnName,
                    DataType = dataType,
                    AllowDbNull = allowDbNull,
                    IsIdentity = isIdentity,
                    IsAutoIncrement = isAutoIncrement
                });
            }

            return result.ToArray();
        }

        /// <summary>
        /// POCO property to SQL column mapping
        /// </summary>
        protected class PocoPropertyToSqlColumnMap
        {
            /// <summary>
            /// 0-based SQL table column index
            /// </summary>
            public int SqlColumnIndex { get; set; }

            /// <summary>
            /// 0-based POCO class property index
            /// </summary>
            public int PocoPropertyIndex { get; set; }
        }

        protected class Mapping
        {
            /// <summary>
            /// SQL data table
            /// </summary>
            public DataTable SqlDataTable { get; set; }

            /// <summary>
            /// SQL schema
            /// </summary>
            public SqlColumnInformation[] SqlSchema { get; set; }

            /// <summary>
            /// POCO schema
            /// </summary>
            public PropertyMappingInfo[] PocoSchema { get; set; }

            /// <summary>
            /// Mapping between properties of the POCO schema and columns of SQL schema
            /// </summary>
            public PocoPropertyToSqlColumnMap[] Map { get; set; }
        }



        /// <summary>
        /// Create mapping from POCO model to SQL table
        /// </summary>
        /// <param name="tableName">target table name</param>
        /// <param name="sqlSchema">SQL table schema</param>
        /// <param name="pocoSchema">POCO schema</param>
        /// <returns></returns>
        protected Mapping CreateMapping(string tableName, SqlColumnInformation[] sqlSchema, PropertyMappingInfo[] pocoSchema)
        {
            var result = new Mapping
            {
                SqlDataTable = new DataTable(tableName),
                SqlSchema = sqlSchema,
                PocoSchema = pocoSchema,
            };

            var mapList = new List<PocoPropertyToSqlColumnMap>();

            for (var i = 0; i < sqlSchema.Length; i++)
            {
                var sqlColumn = sqlSchema[i];

                //pick up POCO property
                for (var j = 0; j < pocoSchema.Length; j++)
                {
                    var pocoProperty = pocoSchema[j];
                    if (pocoProperty.EqualsToMappingInfo(sqlColumn))
                    {
                        //save mapping
                        mapList.Add(new PocoPropertyToSqlColumnMap
                        {
                            SqlColumnIndex = i,
                            PocoPropertyIndex = j
                        });

                        //create column
                        result.SqlDataTable.Columns.Add(sqlColumn.ColumnName, sqlColumn.DataType);
                        break;
                    }
                }
            }

            result.Map = mapList.ToArray();

            return result;
        }

        /// <summary>
        /// Insert the prepared & filled data table into SQL target table
        /// </summary>
        /// <param name="mapping">Prepared & filled data table with mapping information</param>
        /// <returns>true in case of success, otherwise false</returns>
        protected void BulkInsertData(DataTable dataTable, string tableName, int[] columnMapping)
        {
            _circuitBreaker.Action(() =>
            {
                OpenConnection();

                var sqlBulkCopy = new SqlBulkCopy(
                    _connection,
                    SqlBulkCopyOptions.TableLock |
                    SqlBulkCopyOptions.FireTriggers |
                    SqlBulkCopyOptions.UseInternalTransaction,
                    null);

                sqlBulkCopy.DestinationTableName = tableName;
                sqlBulkCopy.BulkCopyTimeout = BulkCopyTimeout;

                if (columnMapping != null)
                {
                    for (var i = 0; i < columnMapping.Length; i++)
                    {
                        sqlBulkCopy.ColumnMappings.Add(i, columnMapping[i]);
                    }
                }

                sqlBulkCopy.WriteToServer(dataTable);
            });
        }


        protected void OpenConnection()
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                _connection = new ConnectionFactory().Create(_connectionString);
                _connection.Open();
            }
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
        }

        public int BulkCopyTimeout = 6000;
        private readonly string _connectionString;
        private SqlConnection _connection;
        private readonly ICircuitBreaker _circuitBreaker;

        /// <summary>
        /// Skip bad rows or throw an error
        /// </summary>
        public bool SkipBadRows = true;

        #region protected stuctures

        /// <summary>
        /// Column mapping info interface
        /// </summary>
        protected interface IColumnMappingInfo
        {
            string ColumnName { get; }
            int? ColumnIndex { get; }
        }

        protected class SqlColumnInformation : IColumnMappingInfo
        {
            public int ColumnOrdinal { get; set; }
            public string ColumnName { get; set; }
            public Type DataType { get; set; }
            public bool AllowDbNull { get; set; }
            public bool IsIdentity { get; set; }
            public bool IsAutoIncrement { get; set; }

            public int? ColumnIndex { get { return ColumnOrdinal; } }

            public override string ToString()
            {
                return String.Format("{0} {1} ({2})", ColumnOrdinal, ColumnName, DataType.Name);
            }
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

            public void CopyMappingInfo(IColumnMappingInfo src)
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

    } //TableMapper

}
