namespace ScriptRunner_Sharp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Mono.Options;

    public class Program
    {
        public static void Main(string[] args)
        {
            OptionException oe;

            //Parse command line options    
            CommandLineWrapper commandLineOptions = null;
            try
            {
                commandLineOptions = new CommandLineWrapper(args);
            }
            catch (OptionException e)
            {
                Console.Error.WriteLine("ScriptRunner start failed:");
                Console.Error.WriteLine(e.Message);

                commandLineOptions.PrintHelp();

                Environment.Exit(0xA0);
            }

            //Set up logging
            try
            {
                DirectoryInfo logDirectory;
                if (!string.IsNullOrEmpty(commandLineOptions.LogDirectory))
                {
                    logDirectory = new DirectoryInfo(commandLineOptions.LogDirectory);
                }
                else
                {
                    //Default the log directory to the current working directory
                    logDirectory = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                }

                //Create a log file in the specified directory and set it up for writing
                Logger.InitialiseLogFile(logDirectory);
            }
            catch (IOException e)
            {
                Console.Error.WriteLine("ScriptRunner failed to initialise log file:");
                Console.Error.WriteLine(e.Message);
                Environment.Exit(1);
            }

            //Set up the logger
            if (commandLineOptions.LogStdOut)
            {
                Logger.logToStandardOut();
            }

            if (commandLineOptions.LogDebug)
            {
                Logger.enableDebugLogging();
            }

            Logger.logAndEcho(ScriptRunnerVersion.getVersionString());

            //Main branch - call the relevant subprocess based on supplied arguments    
            boolean lError = false;
            try
            {
                if (commandLineOptions.hasOption(CommandLineOption.RUN))
                {
                    ScriptRunner.run(commandLineOptions);
                    if (commandLineOptions.hasOption(CommandLineOption.NO_EXEC))
                    {
                        Logger.logAndEcho("-noexec parse completed successfully");
                    }
                    else
                    {
                        Logger.logAndEcho("Promotion completed successfully");
                    }
                }
                else if (commandLineOptions.hasOption(CommandLineOption.BUILD))
                {
                    ScriptBuilder.run(commandLineOptions);
                    Logger.logAndEcho("Build completed successfully");
                }
                else if (commandLineOptions.hasOption(CommandLineOption.INSTALL))
                {
                    Logger.logAndEcho("Installing ScriptRunner");
                    Installer.run(commandLineOptions);
                    //Haul up to latest version
                    Updater.run(commandLineOptions);
                    Logger.logAndEcho("Install completed successfully");
                }
                else if (commandLineOptions.hasOption(CommandLineOption.UPDATE))
                {
                    Logger.logAndEcho("Checking Scriptrunner is up to date");
                    Updater.run(commandLineOptions);
                    Logger.logAndEcho("Update check completed successfully");
                }
                else if (commandLineOptions.hasOption(CommandLineOption.PARSE_SCRIPTS))
                {
                    List<String> lFileList = commandLineOptions.getOptionValues(CommandLineOption.PARSE_SCRIPTS);
                    Logger.logAndEcho("Parsing " + lFileList.size() + " PatchScript" + (lFileList.size() != 1 ? "s" : ""));
                    lError = !PatchScript.printScriptsToStandardOut(new File(System.getProperty("user.dir")), lFileList);
                }

                //Print a message to standard out if warnings were encountered
                int lWarnCount = Logger.getWarningCount();
                if (Logger.getWarningCount() > 0)
                {
                    Logger.logAndEcho(lWarnCount + " warning" + (lWarnCount != 1 ? "s were" : " was") + " detected during execution; please review the log for details");
                }
            }
            catch (Throwable th)
            {
                String timeStamp = Logger.LOG_FILE_LOG_TIMESTAMP_FORMAT.format(new Date());
                Console.Error.WriteLine("[" + timeStamp + "] Error encountered while running ScriptRunner (see log for details):");
                Console.Error.WriteLine(th.Message);
                if (!commandLineOptions.hasOption(CommandLineOption.RUN))
                {
                    //Error will already have been logged by runner; for all others log it now
                    Logger.logError(th);
                }
                lError = true;
            }
            finally
            {
                Logger.finaliseLogs();
            }

            //Exit with the correct code
            Environment.Exit(lError ? 1 : 0);
        }
    }
}
