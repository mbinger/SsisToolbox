using System;

namespace SsisToolbox.Sql.Repository.Attributes
{
    /// <summary>
    /// Defines Timestamp column for data transfer object
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class TimestampAttribute : Attribute
    {
    }
}
