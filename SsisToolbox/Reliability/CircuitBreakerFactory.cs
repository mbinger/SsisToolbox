using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SsisToolbox.Interface;

namespace SsisToolbox.Reliability
{
    public class CircuitBreakerFactory
    {
        public ICircuitBreaker Create()
        {
#if DEBUG
            return new DebugCircuitBreaker();
#else
            return new CircuitBreaker();
#endif
        }
    }
}
