using System;

namespace SsisToolbox.Sql.Repository.Attributes
{
    /// <summary>
    /// Defines table name for data transfer object mapping
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TableAttribute : Attribute
    {
        /// <summary>
        /// Define table name
        /// </summary>
        /// <param name="name">The name</param>
        public TableAttribute(string name)
        {
            Name = name;
        }

        public readonly string Name;
    }
}
