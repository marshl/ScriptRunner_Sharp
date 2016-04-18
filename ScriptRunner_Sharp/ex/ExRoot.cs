
using System;

namespace ScriptRunner_Sharp
{
    /**
     * Root exception class for checked exceptions.
     */
    public class ExRoot : Exception
    {

        private void logThis()
        {
            //    Logger.logInfo("ERROR:::");
            //    Logger.logError(this);
        }

        public ExRoot(string pString, Exception pThrowable) : base(pString, pThrowable)
        {

            logThis();
        }

        public ExRoot(string pString) : base(pString)
        {
            logThis();
        }

        public ExRoot() : base()
        {
            logThis();
        }
    }
}