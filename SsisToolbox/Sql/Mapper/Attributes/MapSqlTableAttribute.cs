using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SsisToolbox.Sql.Mapper.Attributes
{
    /// <summary>
    /// Configures mapping of POCO class to SQL table
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class MapSqlTableAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="columnIndex">Target SQL table 0-based column index</param>
        public MapSqlTableAttribute(int columnIndex)
        {
            ColumnIndex = columnIndex;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="columnName">Target SQL table column name or null when matches the property name</param>
        public MapSqlTableAttribute(string columnName = null)
        {
            ColumnName = columnName;
        }

        public string ColumnName { get; set; }
        public int? ColumnIndex { get; set; }
    }
}
