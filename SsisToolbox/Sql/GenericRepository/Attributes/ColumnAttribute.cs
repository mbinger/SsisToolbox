using System;

namespace SsisToolbox.Sql.Repository.Attributes
{
    /// <summary>
    /// Defines binding name for a column
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ColumnAttribute : Attribute
    {
        public ColumnAttribute(string name)
        {
            Name = name;
        }
        public readonly string Name;
    }
}
