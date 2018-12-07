using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SsisToolbox.Interface
{
    /// <summary>
    /// Circuit breaker
    /// </summary>
    public interface ICircuitBreaker
    {
        void Action(Action action);
        T Action<T>(Func<T> func);
    }
}
