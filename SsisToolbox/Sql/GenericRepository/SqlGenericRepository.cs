using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SsisToolbox.Reliability;
using SsisToolbox.Interface;
using SsisToolbox.Sql.Repository.Attributes;

namespace SsisToolbox.Sql.Repository
{
    /// GenericRepository rev 1.0 from 20.12.2017
    /// <summary>
    /// Generic repository
    /// </summary>
    /// <typeparam name="TDto">Type of data transfer object</typeparam>
    public class SqlGenericRepository<TDto> : IDisposable where TDto : class
    {
        public SqlGenericRepository(string connectionString)
        {
            _circuitBreaker = new CircuitBreakerFactory().Create();

            _dtoType = typeof(TDto);
            _connectionString = connectionString;

            //get table name
            var tableNameAttribute =
                _dtoType.GetCustomAttributes(false)
                    .Where(p => p.GetType() == typeof(TableAttribute))
                    .Cast<TableAttribute>()
                    .FirstOrDefault();

            #region get table name

            if (tableNameAttribute != null)
            {
                TableName = tableNameAttribute.Name;
            }
            else throw new InvalidOperationException(string.Format("The type {0} have not table name definition", _dtoType.Name));

            #endregion

            #region  get mapped properties

            _dtoMappedProperties =
                _dtoType.GetProperties(BindingFlags.Public | BindingFlags.SetProperty | BindingFlags.Instance)
                    .Select(p => new PropertyBinding
                    {
                        Property = p,
                        Column = p.GetCustomAttributes(false).Where(ca => ca.GetType() == typeof(ColumnAttribute))
                        .Cast<ColumnAttribute>()
                        .FirstOrDefault()
                    })
                    .ToArray();

            #endregion

            #region  get ID property

            var resultArr = _dtoMappedProperties.Where(p => p.Property.GetCustomAttributes(false).Any(a => a is IdAttribute)).ToArray();
            if (resultArr.Length > 1)
            {
                throw new Exception(string.Format("The data transfer object '{0}' defines more than one identifying properties", _dtoType.Name));
            }
            _dtoIdProperty = resultArr.FirstOrDefault();
            if (_dtoIdProperty == null)
            {
                throw new Exception(string.Format("The data transfer object '{0}' does not define a identifying property", _dtoType.Name));
            }

            #endregion

            #region  get timestamp property

            resultArr = _dtoMappedProperties.Where(p => p.Property.GetCustomAttributes(false).Any(a => a is TimestampAttribute)).ToArray();
            if (resultArr.Length > 1)
            {
                throw new Exception(string.Format("The data transfer object '{0}' defines more than one timestamp properties", _dtoType.Name));
            }
            _dtoTimestampProperty = resultArr.FirstOrDefault();

            #endregion
        }

        private readonly Type _dtoType;

        /// <summary>
        /// Property binding information
        /// </summary>
        private class PropertyBinding
        {
            public PropertyInfo Property { get; set; }
            public ColumnAttribute Column { get; set; }

            public string ColumnName
            {
                get
                {
                    if (Column != null)
                    {
                        return Column.Name;
                    }
                    return Property.Name;
                }
            }
        }

        private readonly PropertyBinding[] _dtoMappedProperties;
        private readonly PropertyBinding _dtoIdProperty;
        private readonly PropertyBinding _dtoTimestampProperty;

        private readonly string _connectionString;

        protected readonly string TableName;
        protected SqlConnection Connection { get; private set; }
        protected readonly ICircuitBreaker _circuitBreaker;

        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Open the SQL connection
        /// </summary>
        protected void Open()
        {
            if (Connection == null)
            {
                Connection = new ConnectionFactory().Create(_connectionString);
            }

            if (Connection.State != ConnectionState.Open)
            {
                _circuitBreaker.Action(() =>
                {
                    Connection.Open();
                    return true;
                });
            }
        }

        /// <summary>
        /// Close the SQL connection if opened
        /// </summary>
        protected void Close()
        {
            if (Connection != null)
            {
                if (Connection.State != ConnectionState.Closed)
                {
                    Connection.Close();
                }

                Connection = null;
            }
        }

        /// <summary>
        /// Read column value and cast database-specific values (DbNull -> null)
        /// </summary>
        /// <param name="reader">Opened SQL reader</param>
        /// <param name="columnName">Column name to read</param>
        /// <returns></returns>
        protected object Read(SqlDataReader reader, string columnName)
        {
            int columnIndex;
            try
            {
                columnIndex = reader.GetOrdinal(columnName);
            }
            catch (IndexOutOfRangeException)
            {
                //column not found
                throw new Exception(string.Format("Column with name '{0}' not found in the table '{1}'", columnName, TableName));
            }

            //convert DbNull to null
            if (reader.IsDBNull(columnIndex))
            {
                //return defautl value
                return null;
            }

            //get value as object
            var value = reader.GetValue(columnIndex);

            return value;
        }

        /// <summary>
        /// Read row and map to data transfer object
        /// </summary>
        /// <param name="reader">Opened SQL reader</param>
        /// <returns>Mapped data transfer object</returns>
        protected TDto ReadRowMapDto(SqlDataReader reader)
        {
            var result = (TDto)Activator.CreateInstance(_dtoType);

            //get all instance-scoped public properties
            foreach (var propertyBinding in _dtoMappedProperties)
            {
                //read value as object
                var value = Read(reader, propertyBinding.ColumnName);

                //set instance value
                propertyBinding.Property.SetValue(result, value, null);
            }

            return result;
        }

        /// <summary>
        /// Find single record by identity column
        /// </summary>
        /// <param name="identityColumnName">The identity column name</param>
        /// <param name="identityColumnValue">Identity column value</param>
        /// <returns>Mapped data transfer object or null if not found</returns>
        protected TDto FindByIdentityColumn(string identityColumnName, object identityColumnValue)
        {
            Open();

            if (Connection != null && Connection.State == ConnectionState.Open)
            {
                return _circuitBreaker.Action(() =>
                {

                        //prepare command
                        using (var command = new SqlCommand(string.Format(@"
SELECT TOP 1 *
FROM {0}
WHERE [{1}] like @{1}
", TableName, identityColumnName),
    Connection))
                    {
                        command.Parameters.AddWithValue(string.Format("@{0}", identityColumnName), identityColumnValue);

                            //read and map result
                            using (var reader = command.ExecuteReader())
                        {
                            reader.Read();

                            if (!reader.HasRows)
                            {
                                return null;
                            }

                            var result = ReadRowMapDto(reader);
                            return result;
                        }
                    }
                });
            }

            return null;
        }

        /// <summary>
        /// Find single record by specified column
        /// </summary>
        /// <typeparam name="TProperty">Type of mapped to the column property</typeparam>
        /// <param name="pickPropertyExpression">Property expression to pick up the mapped property</param>
        /// <param name="value">Value to be searched for</param>
        /// <returns>Mapped data transfer object or null if not found</returns>
        public TDto FindByColumn<TProperty>(Expression<Func<TDto, TProperty>> pickPropertyExpression, TProperty value)
        {
            var memberExpression = pickPropertyExpression.Body as MemberExpression;
            if (memberExpression == null)
            {
                throw new ArgumentException(
                    @"Pick property expression should be MemberExpression. Be sure, that you specified the type of TProperty correct and it will be not converted to relative type (for example from long to long?)",
                    nameof(pickPropertyExpression));
            }

            //get property name
            var propertyName = memberExpression.Member.Name;

            return FindByIdentityColumn(propertyName, value);
        }

        /// <summary>
        /// Find single record by ID
        /// </summary>
        /// <typeparam name="TKeyColumn">Type of the mapped id property</typeparam>
        /// <param name="idAttributeValue">ID</param>
        /// <returns>Mapped data transfer object or null if not found</returns>
        public TDto FindById<TKeyColumn>(TKeyColumn idAttributeValue)
        {
            //check ID attribute value type
            var idAttributeValueType = typeof(TKeyColumn);
            if (_dtoIdProperty.Property.PropertyType != idAttributeValueType)
            {
                throw new InvalidOperationException(string.Format("Invalid id attribute value type '{0}. Type '{1}' expected", idAttributeValueType.Name, _dtoIdProperty.Property.PropertyType.Name));
            }

            return FindByIdentityColumn(_dtoIdProperty.ColumnName, idAttributeValue);
        }

        /// <summary>
        /// Update the record
        /// </summary>
        /// <param name="item">Mapped data transfer object</param>
        /// <returns>true - the record updated successfully. false - the record not found or its timestamp changed since last load</returns>
        public bool Update(TDto item)
        {
            //get all updatable properties
            var propertiesToUpdate = _dtoMappedProperties
                    .Where(p => p != _dtoIdProperty && p != _dtoTimestampProperty)
                    .ToArray();

            //[a]=@a, [b]=@b, [c]=@c
            var propertiesToUpdateSql = string.Join(",", propertiesToUpdate.Select(p => string.Format("[{0}]=@{0}", p.ColumnName)));

            var whereSql = string.Format("[{0}]=@{0}", _dtoIdProperty.ColumnName);
            if (_dtoTimestampProperty != null)
            {
                whereSql += string.Format(" and [{0}]=@{0}", _dtoTimestampProperty.ColumnName);
            }

            //prepare query
            var query = string.Format("UPDATE {0} SET {1} WHERE {2}", TableName, propertiesToUpdateSql, whereSql);

            Open();

            if (Connection != null && Connection.State == ConnectionState.Open)
            {
                using (var command = new SqlCommand(query, Connection))
                {
                    //add parameters for values and where clausel
                    foreach (var propertyBinding in _dtoMappedProperties)
                    {
                        var parameterName = string.Format("@{0}", propertyBinding.ColumnName);
                        var parameterValue = propertyBinding.Property.GetValue(item, null) ?? DBNull.Value;
                        command.Parameters.AddWithValue(parameterName, parameterValue);
                    }

                    //execute command
                    var affectedRowsCount = _circuitBreaker.Action(() => command.ExecuteNonQuery());

                    //check affected rows count
                    return affectedRowsCount > 0;
                }
            }

            return false;
        }
    }
}