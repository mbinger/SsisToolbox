using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SsisToolbox.Interface
{
    /// <summary>
    /// Interface for events object wrapper
    /// </summary>
    public interface IDts
    {
        void FireInformation(string subComponent, string description);
        void FireWarning(string subComponent, string description);
        void FireError(string subComponent, string description);

        int SuccessCode { get; }
        int FailureCode { get; }
    }
}
