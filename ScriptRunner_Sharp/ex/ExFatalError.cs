using System;

namespace ScriptRunner_Sharp
{
    /**
     * General exception class for fatal errors which are caused by user input but are not expected to be handled.
     */
    public class ExFatalError : ExRuntimeRoot
    {
        public ExFatalError(String pString, Exception pThrowable) : base(pString, pThrowable)
        {
        }

        public ExFatalError(String pString) : base(pString)
        {
        }

        public ExFatalError() : base()
        {
        }
    }
}