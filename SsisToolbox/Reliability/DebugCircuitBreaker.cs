using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SsisToolbox.Interface;

namespace SsisToolbox.Reliability
{
    /// <summary>
    /// Debug version of circuit breaker that call the code immediate
    /// </summary>
    public class DebugCircuitBreaker : ICircuitBreaker
    {
        public void Action(Action action)
        {
            action();
        }

        public T Action<T>(Func<T> func)
        {
            return func();
        }
    }
}
