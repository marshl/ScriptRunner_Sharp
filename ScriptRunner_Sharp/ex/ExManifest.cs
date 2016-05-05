using System;

namespace ScriptRunner_Sharp
{

    /**
     * Exception class for any errors encountered when parsing a manifest file.
     */
    public class ExManifest : Exception
    {
        public ExManifest(string pString, Exception pThrowable) : base(pString, pThrowable)
        {
        }

        public ExManifest(string pString) : base(pString)
        {
        }

        public ExManifest() : base()
        {
        }
    }
}