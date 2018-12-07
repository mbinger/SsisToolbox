using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SsisToolbox.Interface;

namespace SsisToolbox.Reliability
{
    /// <summary>
    /// Implementation of  circuit breaker pattern
    /// Ver 18.09.2018
    /// </summary>
    public class CircuitBreaker : ICircuitBreaker
    {
        public CircuitBreaker()
        {
            _warningThresold = 3;
            _closeThresold = 6;
            _failureThresold = 10;

            _openedWait = 0;
            _halfOpenedWait = 5000;
            _closedWait = 300000;

            Reset();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="warningThresold">Error count thresold to switch from "open" state into "half opened"</param>
        /// <param name="closeThresold">Error count thresold to switch from "half opened" state into "closed"</param>
        /// <param name="failureThresold">Error count thresold to throw an exception</param>
        /// <param name="openedWait">Wait interval in ms between repeats of action in "opened" state</param>
        /// <param name="halfOpenedWait">Wait interval in ms between repeats of action in "half opened" state</param>
        /// <param name="closedWait">Wait interval in ms between repeats of action in "closed" state</param>
        public CircuitBreaker(int warningThresold, int closeThresold, int failureThresold, int openedWait, int halfOpenedWait, int closedWait)
        {
            if (warningThresold <= 0)
            {
                throw new ArgumentException("Warning thresold must be greater than zero", "warningThresold");
            }
            if (closeThresold <= warningThresold)
            {
                throw new ArgumentException("Close thresold must be greater than warning thresold", "closeThresold");
            }
            if (failureThresold <= closeThresold)
            {
                throw new ArgumentException("Failure thresold must be greater than close thresold", "failureThresold");
            }
            if (openedWait < 0)
            {
                throw new ArgumentException("Opened wait interval can not be negative", "openedWait");
            }
            if (halfOpenedWait <= openedWait)
            {
                throw new ArgumentException("Half opened wait interval must be greater than opened wait interval", "halfOpenedWait");
            }
            if (closedWait <= halfOpenedWait)
            {
                throw new ArgumentException("Closed opened wait interval must be greater than half opened wait interval", "closedWait");
            }

            _warningThresold = warningThresold;
            _closeThresold = closeThresold;
            _failureThresold = failureThresold;

            _openedWait = openedWait;
            _halfOpenedWait = halfOpenedWait;
            _closedWait = closedWait;

            Reset();
        }


        public void Action(Action action)
        {
            Action(() =>
            {
                action();
                return true;
            });
        }

        /// <summary>
        /// Return result or throw an exception
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public T Action<T>(Func<T> func)
        {
            while (true)
            {
                try
                {
                    //wait
                    if (_waitBeforeNextAction > 0)
                    {
                        Thread.Sleep(_waitBeforeNextAction);
                    }

                    //execute action
                    var result = func();

                    //successfully 
                    OnSuccess();

                    //return the result
                    return result;
                }
                catch (InvalidOperationException ex)
                {
                    //immediate re-throw
                    throw ex;
                }
                catch (Exception ex)
                {
                    //on failure
                    var exceptionToReThrow = OnFailure(ex);
                    if (exceptionToReThrow != null)
                    {
                        //re-throw error
                        throw exceptionToReThrow;
                    }
                }
                finally
                {
                    //configure wait time for the next action
                    SetWaitBeforeNextAction();
                }
            }
        }

        protected virtual void OnSuccess()
        {
            _successCount++;
            _errCount = 0;

            switch (_state)
            {
                case State.Opened:
                    //nothing to do
                    break;

                case State.HalfOpened:
                    if (_successCount >= _warningThresold)
                    {
                        Reset();
                    }
                    break;

                case State.Closed:
                    _state = State.HalfOpened;
                    break;
            }
        }

        protected virtual Exception OnFailure(Exception ex)
        {
            _errCount++;
            _successCount = 0;

            switch (_state)
            {
                case State.Opened:
                    if (_errCount >= _warningThresold)
                    {
                        _state = State.HalfOpened;
                    }
                    break;

                case State.HalfOpened:
                    if (_errCount >= _closeThresold)
                    {
                        _state = State.Closed;
                    }
                    break;

                case State.Closed:
                    if (_errCount >= _failureThresold)
                    {
                        _errCount = _warningThresold; //reset error count to warning thresold
                        return ex;
                    }
                    break;
            }

            return null;
        }

        public void Reset()
        {
            _state = State.Opened;
            _errCount = 0;
            _successCount = 0;
        }

        private void SetWaitBeforeNextAction()
        {
            switch (_state)
            {
                case State.Opened:
                    _waitBeforeNextAction = _openedWait;
                    return;

                case State.HalfOpened:
                    _waitBeforeNextAction = _halfOpenedWait;
                    return;

                case State.Closed:
                    _waitBeforeNextAction = _closedWait;
                    return;
            }
        }

        private int _warningThresold;
        private int _closeThresold;
        private int _failureThresold;

        private int _openedWait;
        private int _halfOpenedWait;
        private int _closedWait;

        private int _successCount;
        private int _errCount;
        private State _state;
        private int _waitBeforeNextAction = 0;

        private enum State
        {
            Opened,
            HalfOpened,
            Closed
        }
    }
}
