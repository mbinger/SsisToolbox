using System;

namespace SsisToolbox.Sql.Repository.Attributes
{
    /// <summary>
    /// Defines ID column for data transfer object
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IdAttribute : Attribute
    {
    }
}
