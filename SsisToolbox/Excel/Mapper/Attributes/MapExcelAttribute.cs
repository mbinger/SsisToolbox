using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SsisToolbox.Excel.Mapper.Attributes
{
    /// <summary>
    /// Configures Excel parsing to the typed POCO class
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class MapExcelAttribute : Attribute
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="columnIndex">Source Excel sheet 0-based column index</param>
        public MapExcelAttribute(int columnIndex)
        {
            ColumnIndex = columnIndex;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="columnName">Source Excel sheet column name like "Abteilung" or its alphabetical name like "$AB"</param>
        public MapExcelAttribute(string columnName = null)
        {
            if (columnName.StartsWith("$"))
            {
                ColumnIndex = ExcelColumnNameToNumber(columnName.Replace("$", ""));
            }
            else
            {
                ColumnName = columnName;
            }
        }

        private int ExcelColumnNameToNumber(string columnName)
        {
            columnName = columnName.ToUpperInvariant();

            int sum = 0;

            for (int i = 0; i < columnName.Length; i++)
            {
                sum *= 26;
                sum += (columnName[i] - 'A' + 1);
            }

            return sum - 1;
        }

        public string ColumnName { get; set; }
        public int? ColumnIndex { get; set; }
    }
}
