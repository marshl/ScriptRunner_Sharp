

namespace ScriptRunner_Sharp
{
    using System;
    /**
     * Exception class for any errors caused by text file parsing.
     */
    public class ExParser : ExRoot {

        public ExParser() : base() {
        }
        
        public ExParser(String pString, Exception pThrowable)
        {
            
        }

        public ExParser(string pString) : base(pString)
        {

        }
    }

}