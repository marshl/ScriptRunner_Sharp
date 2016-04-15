using System;

/**
 * Exception class for errors which have been caused by internal assertion failures or programming errors.
 */
namespace ScriptRunner_Sharp
{
    public class ExInternal : Exception
    {
        public ExInternal(String pString, Exception pThrowable) : base(pString, pThrowable)
        {

        }

        public ExInternal(String pString) : base(pString)
        {

        }

        public ExInternal() : base()
        {
        }
    }

}