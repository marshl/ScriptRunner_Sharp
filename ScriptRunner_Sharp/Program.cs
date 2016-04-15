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
            string buildDirectory = string.Empty;
            string runDirectory = string.Empty;
            bool install = false;
            bool update = false;

            List<string> parseOptions = new List<string>();
            List<string> cursor = null;
            bool parse = false;
            string logDirectory = string.Empty;
            bool logStdOut = false;
            bool logDebug = false;
            bool skipVersionCheck = false;
            bool skipHashCheck = false;
            bool skipExecution = false;
            string promoteuser = String.Empty;
            string promotePassword = string.Empty;
            string jdbcConnectionString = string.Empty;
            string newPromoteUser = "SYS";
            string newPromotePassword = string.Empty;
            string databaseHost = string.Empty;
            string databasePort = string.Empty;
            string databaseSid = string.Empty;
            string databaseService = string.Empty;

            bool connectAsSysDba = false;

            string outfile = string.Empty;
            string promotionLaabel = string.Empty;

            string additionalProperties = string.Empty;

            bool noUnimplicatedFiles = false;

            bool showHelp = false;

            OptionSet optionSet = new OptionSet()
            {
                { "b|build=",
                    "Builds a promotion archive from the given source directory.",
                  (string v) => buildDirectory = v },
                { "r|run=",
                    "Runs a promotion from given source archive or directory.",
                  (string v) => runDirectory = v },
                { "i|install",
                    "Installs ScriptRunner metadata tables (requires SYSDBA privileges).",
                   v => install = v != null },
                { "u|update",
                    "Updates ScriptRunner metadata tables to the latest version.",
                   v => update = v != null },
                { "parse",
                    "Parses patch scripts and outputs the result to standard out.",
                   v => cursor = parseOptions },
                { "<>",
                   (string v) => cursor.Add(v) },
                { "logdir=",
                    "Directory to write log file to. Default is current directory.",
                   (string v) => logDirectory = v },
                { "logstdout",
                    "Log all output to standard out in addition to the log file.",
                   v => logStdOut = v != null },
                { "logdebug",
                    "Turns verbose debug logging on.",
                   v => logDebug = v != null },
                { "noversioncheck",
                    "(Run only) Skips the ScriptRunner version verification.",
                   v => skipVersionCheck = v != null },
                { "nohashcheck",
                    "(Run only) Skips checking file hashes against entries the manifest.",
                   v => skipHashCheck = v != null },
                { "noexec",
                    "(Run only) Does not execute the promote but produces output showing what would be run.",
                   v => skipExecution = v != null },

                { "user=",
                   "Specify the database user to connect as (default is " + DatabaseConnection.DEFAULT_PROMOTE_USER + ")",
                   (string v) => promoteUser = v },
                { "password=",
                   "Specify the password for the database user.If not specified this will be prompted for.",
                  (string v) => promotePassword = v },
                { "jdbc",
                   "A full JDBC connect string for establishing a database connection.",
                  (string v) => jdbcConnectionString = v },

                 { "newpromoteuser=",
                   "(install only) The new promotion user to create.",
                  (string v) => newPromoteUser = v },
                 { "newpromotepassword=",
                   "(install only) The password to use for the new promote user",
                  (string v) => newPromotePassword = v },

                 { "host=",
                   "Database hostname.",
                  (string v) => databaseHost = v },
                { "port=",
                   "Database port.",
                  (string v) => databasePort = v },
                { "sid=",
                   "Database SID.",
                  (string v) => databaseSid = v },
                { "service=",
                   "Database service name.",
                  (string v) => databaseService = v },

                { "sysdba",
                    "Connect to the database as SYSDBA.",
                   v => connectAsSysDba = v != null },

                { "outfile=",
                   "(Build only) File path where the output will be written to. Default is {CURRENT_DIR}/{PROMOTE_LABEL}.zip",
                  (string v) => outfile = v },

                { "label=",
                   "(Build only) Promotion label for builder.",
                  (string v) => promotionLaabel = v },

                { "props=",
                   "(Build only) Location of the additional properties file for the builder.",
                  (string v) => additionalProperties = v },

                { "nounimplicatedfiles",
                   "(Build only) Error (rather than warn) if files are found in source directory but not implicated by manifest builder rules.",
                  v => noUnimplicatedFiles = v != null },

                { "h|help",  "show this message and exit",
                    v => showHelp = v != null },
            };

            List<string> extraOptions = optionSet.Parse(args);

            if (showHelp)
            {
                optionSet.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (extraOptions.Count > 0)
            {
                optionSet.WriteOptionDescriptions(Console.Out);
                return;
            }

            OptionException oe;

            //Parse command line options    
            CommandLineWrapper lCommandLineOptions = null;
            try
            {
                lCommandLineOptions = new CommandLineWrapper(args);
            }
            catch (ParseException e)
            {
                Console.Error.WriteLine("ScriptRunner start failed:");
                Console.Error.WriteLine(e.Message);

                CommandLineWrapper.printHelp();

                Environment.Exit(1);
            }

            //Set up logging
            try
            {
                File lLogDir;
                if (lCommandLineOptions.hasOption(CommandLineOption.LOG_DIRECTORY))
                {
                    lLogDir = new File(lCommandLineOptions.getOption(CommandLineOption.LOG_DIRECTORY));
                }
                else
                {
                    //Default the log directory to the current working directory
                    lLogDir = new File(System.getProperty("user.dir"));
                }

                //Create a log file in the specified directory and set it up for writing
                Logger.initialiseLogFile(lLogDir);
            }
            catch (IOException e)
            {
                Console.Error.WriteLine("ScriptRunner failed to initialise log file:");
                Console.Error.WriteLine(e.Message);
                Environment.Exit(1);
            }

            //Set up the logger
            if (lCommandLineOptions.hasOption(CommandLineOption.LOG_STANDARD_OUT))
            {
                Logger.logToStandardOut();
            }

            if (lCommandLineOptions.hasOption(CommandLineOption.LOG_DEBUG))
            {
                Logger.enableDebugLogging();
            }

            Logger.logAndEcho(ScriptRunnerVersion.getVersionString());

            //Main branch - call the relevant subprocess based on supplied arguments    
            boolean lError = false;
            try
            {
                if (lCommandLineOptions.hasOption(CommandLineOption.RUN))
                {
                    ScriptRunner.run(lCommandLineOptions);
                    if (lCommandLineOptions.hasOption(CommandLineOption.NO_EXEC))
                    {
                        Logger.logAndEcho("-noexec parse completed successfully");
                    }
                    else
                    {
                        Logger.logAndEcho("Promotion completed successfully");
                    }
                }
                else if (lCommandLineOptions.hasOption(CommandLineOption.BUILD))
                {
                    ScriptBuilder.run(lCommandLineOptions);
                    Logger.logAndEcho("Build completed successfully");
                }
                else if (lCommandLineOptions.hasOption(CommandLineOption.INSTALL))
                {
                    Logger.logAndEcho("Installing ScriptRunner");
                    Installer.run(lCommandLineOptions);
                    //Haul up to latest version
                    Updater.run(lCommandLineOptions);
                    Logger.logAndEcho("Install completed successfully");
                }
                else if (lCommandLineOptions.hasOption(CommandLineOption.UPDATE))
                {
                    Logger.logAndEcho("Checking Scriptrunner is up to date");
                    Updater.run(lCommandLineOptions);
                    Logger.logAndEcho("Update check completed successfully");
                }
                else if (lCommandLineOptions.hasOption(CommandLineOption.PARSE_SCRIPTS))
                {
                    List<String> lFileList = lCommandLineOptions.getOptionValues(CommandLineOption.PARSE_SCRIPTS);
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
                if (!lCommandLineOptions.hasOption(CommandLineOption.RUN))
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
