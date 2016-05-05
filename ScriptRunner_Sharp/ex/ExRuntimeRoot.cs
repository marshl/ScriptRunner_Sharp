

namespace ScriptRunner_Sharp
{
    using System;
    /**
     * Root runtime exception class for unchecked exceptions.
     */
    public class ExRuntimeRoot : Exception
    {
        public ExRuntimeRoot(string pString, Exception pThrowable) : base(pString, pThrowable)
        {

        }

        public ExRuntimeRoot(string pString) : base(pString)
        {

        }

        public ExRuntimeRoot() : base()
        {

        }
    }

}