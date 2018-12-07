using System;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;

namespace SsisToolbox.Sql
{
    /// <summary>
    /// Connection factory 24.08.2018
    /// </summary>
    public class ConnectionFactory
    {
        /// <summary>
        /// Create sql connection from compatible connection string
        /// </summary>
        /// <param name="compatibleConnectionString">Compatible Ado.Net, native Sql client, Ole Db or Odbc connection string</param>
        /// <returns></returns>
        public SqlConnection Create(string compatibleConnectionString)
        {
            //try Sql
            var connection = CreateSqlConnection(compatibleConnectionString);
            if (connection != null)
            {
                return connection;
            }

            //try Ole Db
            connection = CreateOleDbConnection(compatibleConnectionString);
            if (connection != null)
            {
                return connection;
            }

            //try Odbc
            connection = CreateOdbcConnection(compatibleConnectionString);
            if (connection != null)
            {
                return connection;
            }

            throw new ApplicationException("The connection string is not compatible");
        }

        /// <summary>
        /// Create sql connection from SQL connection string
        /// </summary>
        /// <returns>Connection or null</returns>
        private SqlConnection CreateSqlConnection(string sqlConnectionString)
        {
            try
            {
                return new SqlConnection(sqlConnectionString);
            }
            catch (Exception)
            {
            }
            return null;
        }

        /// <summary>
        /// Create sql connection from Ole DB connection string
        /// </summary>
        /// <returns></returns>
        private SqlConnection CreateOleDbConnection(string oleDbConnectionString)
        {
            try
            {
                var normalizedConnectionString = oleDbConnectionString.ToLower();
                var srcBuilder = new OleDbConnectionStringBuilder(oleDbConnectionString);
                var dstBuilder = new SqlConnectionStringBuilder();
                foreach (var key in srcBuilder.Keys)
                {
                    var keyStr = key.ToString();
                    if (normalizedConnectionString.Contains(keyStr.ToLower()))
                    {
                        try
                        {
                            dstBuilder.Add(keyStr, srcBuilder[keyStr]);
                        }
                        catch (Exception)
                        {
                            //skip not supported keys
                        }
                    }
                }

                return new SqlConnection(dstBuilder.ConnectionString);
            }
            catch (Exception)
            {
            }
            return null;
        }

        /// <summary>
        /// Create sql connection from Odbc DB connection string
        /// </summary>
        /// <returns></returns>
        private SqlConnection CreateOdbcConnection(string odbcConnectionString)
        {
            try
            {
                var normalizedConnectionString = odbcConnectionString.ToLower();
                var srcBuilder = new OdbcConnectionStringBuilder(odbcConnectionString);
                var dstBuilder = new SqlConnectionStringBuilder();
                foreach (var key in srcBuilder.Keys)
                {
                    var keyStr = key.ToString();
                    if (normalizedConnectionString.Contains(keyStr.ToLower()))
                    {
                        try
                        {
                            dstBuilder.Add(keyStr, srcBuilder[keyStr]);
                        }
                        catch (Exception)
                        {
                            //skip not supported keys
                        }
                    }
                }

                return new SqlConnection(dstBuilder.ConnectionString);
            }
            catch (Exception)
            {
            }
            return null;
        }
    }
}