
using System;
using System.Collections.Generic;
using System.IO;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace ScriptRunner_Sharp
{
    /**
     * Provider of a simple logging interface for ScriptRunner. Multiple log destinations are supported and there is basic support
     * for different logging levels.
     */
    public class Logger
    {
        private static readonly List<StreamWriter> gLogWriterList = new List<StreamWriter>();
        private static bool gLogToStandardOut = false;
        private static bool gLogFileInitialised = false;

        private static string LOG_FILE_NAME_PREFIX = "ScriptRunner-";
        private const string LOG_FILE_NAME_SUFFIX = ".log";
        private const string LOG_FILE_DATE_FORMAT = "{yyyy-MM-dd_HHmmss}";
        public const string LOG_FILE_LOG_TIMESTAMP_FORMAT = "{yyyy-MM-dd HH:mm:ss.SSS}";

        private static FileInfo gLogFile = null;

        public static int gWarningCount { get; private set; } = 0;

        private static bool gLogDebug = false;

        /**
         * Enables standard out logging.
         */
        public static void logToStandardOut()
        {
            if (!gLogToStandardOut)
            {
                gLogWriterList.Add(new StreamWriter(Console.OpenStandardOutput()));
                gLogToStandardOut = true;
            }
        }

        /**
         * Turns debug logging on.
         */
        public static void enableDebugLogging()
        {
            gLogDebug = true;
        }

        /**
         * Adds a new log writer.
         * @param pWriter Writer to add.
         */
        public static void addLogWriter(StreamWriter writer)
        {
            gLogWriterList.Add(writer);
        }

        /**
         * Removes a log writer from the list.
         * @param pWriter Writer to remove.
         */
        public static void removeLogWriter(StreamWriter writer)
        {
            gLogWriterList.Remove(writer);
        }

        /**
         * Writes all output previously logged to the log file to a database CLOB.
         * @param pClob Clob to write to.
         */
        /*public static void writeLogToClob(OracleClob pClob)
        {
            StreamWriter lClobWriter;
            try
            {
                //lClobWriter = pClob.setCharacterStream(0);
                StreamReader sr = new StreamReader(gLogFile.FullName);
                string contents = sr.ReadToEnd();
                byte[] bytes = new byte[contents.Length * sizeof(char)];
                System.Buffer.BlockCopy(contents.ToCharArray(), 0, bytes, 0, bytes.Length);
                pClob.Write(bytes, 0, bytes.Length);

                IOUtils.copy(new FileInputStream(gLogFile), lClobWriter);
                lClobWriter.close();
            }
            catch (IOException e)
            {
                throw new ExInternal("Failed to copy output log to CLOB", e);
            }
            catch (SQLException e)
            {
                throw new ExInternal("Failed to initialise output CLOB", e);
            }
        }*/

        /**
         * Creates a log file in the given directory. The log file is given a default name which includes the datestamp with a
         * resolution of seconds to provide uniqueness.
         * @param pLogDirectory Directory to create log file in.
         * @throws IOException If the log file cannot be created.
         */
        public static void InitialiseLogFile(DirectoryInfo logDirectory)
        {
            if (!gLogFileInitialised)
            {
                string lLogFileName = LOG_FILE_NAME_PREFIX + string.Format(LOG_FILE_DATE_FORMAT, System.DateTime.Now) + LOG_FILE_NAME_SUFFIX;
                gLogFile = new FileInfo(Path.Combine(logDirectory.FullName, lLogFileName));

                gLogWriterList.Add(new StreamWriter(gLogFile.FullName));
                gLogFileInitialised = true;
            }
        }

        /**
         * Internal method for logging a message to all loggers.
         * @param pString Message.
         */
        private static void log(string pString)
        {
            foreach (StreamWriter writer in gLogWriterList)
            {
                String timeStamp = string.Format(LOG_FILE_LOG_TIMESTAMP_FORMAT, DateTime.Now);
                try
                {
                    writer.WriteLine($"[{timeStamp}] {pString}");
                    writer.Flush();
                }
                catch (IOException e)
                {
                    throw new ExInternal("Logging exception", e);
                }
            }
        }

        /**
         * Logs a debug message which will only be printed if debug logging is enabled.
         * @param pMessage Message to log.
         */
        public static void logDebug(String pMessage)
        {
            if (gLogDebug)
            {
                log(pMessage);
            }
        }

        /**
         * Logs the message to all loggers and prints to standard out, even if standard out logging is disabled.
         * @param pString Message to log.
         */
        public static void logAndEcho(String pString)
        {
            log(pString);
            if (!gLogToStandardOut)
            {
                Console.Out.WriteLine(pString);
            }
        }

        /**
         * Gets the number of warnings which have occurred so far.
         * @return Warning count.
         */
        public static int getWarningCount()
        {
            return gWarningCount;
        }

        /**
         * Logs a message to all loggers using a PrintWriter format mask.
         * @param pFormatMask Format mask.
         * @param pArgs Arguments for format mask.
         */
        public static void logInfoFormatted(String pFormatMask, params Object[] pArgs)
        {
            logInfo(string.Format(pFormatMask, pArgs));
        }

        /**
         * Logs general information to all loggers.
         * @param pMessage Message to log.
         */
        public static void logInfo(String pMessage)
        {
            log(pMessage);
        }

        /**
         * Logs a warning message. If warnings are logged, the user is notified at the end of the run.
         * @param pMessage Warning message to log.
         */
        public static void logWarning(String pMessage)
        {
            gWarningCount++;
            log("***WARNING***\n" + pMessage);
        }

        /**
         * Prints the stacktrace of an error to each logger.
         * @param pError Error to log.
         */
        public static void logError(Exception pError)
        {
            //Loop through every logger to print stack trace information
            foreach( StreamWriter writer in gLogWriterList)
            {
                Console.WriteLine(pError);
                try
                {
                    writer.Flush();
                }
                catch (IOException e)
                {
                    throw new ExInternal("Logging exception", e);
                }
            }
        }

        /**
         * Closes all log writers.
         */
        public static void finaliseLogs()
        {
            foreach (StreamWriter writer in gLogWriterList)
            {
                try
                {
                    writer.Close();
                }
                catch (IOException e)
                {
                    throw new ExInternal("Logging exception", e);
                }
            }
        }

    }
}