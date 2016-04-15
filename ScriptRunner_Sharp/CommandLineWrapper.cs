/**
 * Wrapper for the Apache Commons CLI library which allows command line options to be resolved by an enum rather than a String.
 * Also provides the ability to override or set options which were not originally specified on the command line. <br/><br/>
 *
 * Static methods are also available for reading arguments and passwords from standard in.
 */
namespace ScriptRunner_Sharp
{
    using System;
    using System.Collections.Generic;
    using Mono.Options;

    public class CommandLineWrapper
    {
        public string BuildDirectory { get; private set; }
        public string RunDirectory { get; private set; }
        public bool Install { get; private set; }
        public bool Update { get; private set; }

        public List<string> parseOptions { get; private set; } = new List<string>();
        private List<string> cursor;

        public string LogDirectory { get; private set; }
        public bool LogStdOut { get; private set; }
        public bool LogDebug { get; private set; }
        public bool SkipVersionCheck { get; private set; }
        public bool SkipHashCheck { get; private set; }
        public bool SkipExecution { get; private set; }
        public string Promoteuser { get; private set; }
        public string PromotePassword { get; private set; }
        public string JdbcConnectionString { get; private set; }
        public string NewPromoteUser { get; private set; } = "SYS";
        public string NewPromotePassword { get; private set; }
        public string DatabaseHost { get; private set; }
        public string DatabasePort { get; private set; }
        public string DatabaseSid { get; private set; }
        public string DatabaseService { get; private set; }

        public bool ConnectAsSysDba { get; private set; }

        public string Outfile { get; private set; }
        public string PromotionLaabel { get; private set; }

        public string AdditionalProperties { get; private set; }

        public bool NoUnimplicatedFiles { get; private set; }

        private bool showHelp = false;

        private OptionSet optionSet;

        public CommandLineWrapper(string[] args)
        {
            this.optionSet = new OptionSet()
            {
                { "b|build=",
                    "Builds a promotion archive from the given source directory.",
                  (string v) => BuildDirectory = v },
                { "r|run=",
                    "Runs a promotion from given source archive or directory.",
                  (string v) => RunDirectory = v },
                { "i|install",
                    "Installs ScriptRunner metadata tables (requires SYSDBA privileges).",
                   v => Install = v != null },
                { "u|update",
                    "Updates ScriptRunner metadata tables to the latest version.",
                   v => Update = v != null },
                { "parse",
                    "Parses patch scripts and outputs the result to standard out.",
                   v => cursor = parseOptions },
                { "<>",
                   (string v) => cursor.Add(v) },
                { "logdir=",
                    "Directory to write log file to. Default is current directory.",
                   (string v) => LogDirectory = v },
                { "logstdout",
                    "Log all output to standard out in addition to the log file.",
                   v => LogStdOut = v != null },
                { "logdebug",
                    "Turns verbose debug logging on.",
                   v => LogDebug = v != null },
                { "noversioncheck",
                    "(Run only) Skips the ScriptRunner version verification.",
                   v => SkipVersionCheck = v != null },
                { "nohashcheck",
                    "(Run only) Skips checking file hashes against entries the manifest.",
                   v => SkipHashCheck = v != null },
                { "noexec",
                    "(Run only) Does not execute the promote but produces output showing what would be run.",
                   v => SkipExecution = v != null },

                { "user=",
                   "Specify the database user to connect as (default is " + DatabaseConnection.DEFAULT_PROMOTE_USER + ")",
                   (string v) => promoteUser = v },
                { "password=",
                   "Specify the password for the database user.If not specified this will be prompted for.",
                  (string v) => PromotePassword = v },
                { "jdbc",
                   "A full JDBC connect string for establishing a database connection.",
                  (string v) => JdbcConnectionString = v },

                 { "newpromoteuser=",
                   "(install only) The new promotion user to create.",
                  (string v) => NewPromoteUser = v },
                 { "newpromotepassword=",
                   "(install only) The password to use for the new promote user",
                  (string v) => NewPromotePassword = v },

                 { "host=",
                   "Database hostname.",
                  (string v) => DatabaseHost = v },
                { "port=",
                   "Database port.",
                  (string v) => DatabasePort = v },
                { "sid=",
                   "Database SID.",
                  (string v) => DatabaseSid = v },
                { "service=",
                   "Database service name.",
                  (string v) => DatabaseService = v },

                { "sysdba",
                    "Connect to the database as SYSDBA.",
                   v => ConnectAsSysDba = v != null },

                { "outfile=",
                   "(Build only) File path where the output will be written to. Default is {CURRENT_DIR}/{PROMOTE_LABEL}.zip",
                  (string v) => Outfile = v },

                { "label=",
                   "(Build only) Promotion label for builder.",
                  (string v) => PromotionLaabel = v },

                { "props=",
                   "(Build only) Location of the additional properties file for the builder.",
                  (string v) => AdditionalProperties = v },

                { "nounimplicatedfiles",
                   "(Build only) Error (rather than warn) if files are found in source directory but not implicated by manifest builder rules.",
                  v => NoUnimplicatedFiles = v != null },

                { "h|help",  "show this message and exit",
                    v => showHelp = v != null },
            };

            List<string> extraOptions = optionSet.Parse(args);

            if (showHelp)
            {
                this.PrintHelp();   
                return;
            }

            if (extraOptions.Count > 0)
            {
                this.PrintHelp();
                return;
            }
        }

        public void PrintHelp()
        {
            optionSet.WriteOptionDescriptions(Console.Out);
        }

        public string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key != ConsoleKey.Backspace)
                {
                    Console.Write("*");
                    password += info.KeyChar;
                }
                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        // remove one character from the list of password characters
                        password = password.Substring(0, password.Length - 1);
                        // get the location of the cursor
                        int pos = Console.CursorLeft;
                        // move the cursor to the left by one character
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        // replace it with space
                        Console.Write(" ");
                        // move the cursor to the left by one character again
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                    }
                }
                info = Console.ReadKey(true);
            }

            // add a new line because user pressed enter at the end of their password
            Console.WriteLine();
            return password;
        }
    }
}