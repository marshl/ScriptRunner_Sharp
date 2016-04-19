

namespace ScriptRunner_Sharp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    /**
     * A PatchScript represents a parsed patch script file, which is used to promote DDL and DML changes to the database.
     * They may also contain instructions to ScriptRunner to control how to connect to the database and when to commit or
     * rollback transactions.<br/><br/>
     *
     * PatchScripts are composed of one or more {@link ScriptExecutable}s which should be executed in order. A PatchScript
     * is uniquely identified by a faceted filename, which is parsed when this object is created.
     */
    public class PatchScript
    {

        public string PatchLabel { get; }
        public int PatchNumber { get; }
        public string Description { get; }
        public List<ScriptExecutable> ExecutableList { get; }
        public string PatchFileHash { get; }
        public int PromotionSequencePosition { get; }
        public string OriginalPatchString { get; }
        public string FileVersion { get; }
        private static readonly Regex FILENAME_PATTERN = new Regex("^([A-Z]{5,})([0-9]{4,}) *\\((.+)\\) *\\.sql$", RegexOptions.Compiled);//, RegexOptions.IgnoreCase removed
        private const int FILENAME_PATTERN_TYPE_GROUP = 1;
        private const int FILENAME_PATTERN_NUMBER_GROUP;
        private const int FILENAME_PATTERN_DESCRIPTION_GROUP = 3;
        private static string PRINT_STATEMENT_DIVIDER = "\n========================================\n";

        /**
         * Prints the parsed contents of each PatchScript in the given path list to standard out. Each statement in the PatchScript
         * is seperated by an obvious string delimiter. Any errors encountered during the parse are also printed to standard out
         * and are not re-thrown. This output is for debugging purposes only and should not be processed programatically.
         * @param pBaseDirectory Base directory for relative path evalulation.
         * @param pScriptPathList List of paths to all required PathScripts.
         * @return True if parsing was successful, false otherwise.
         */
        public static bool printScriptsToStandardOut(DirectoryInfo pBaseDirectory, List<string> pScriptPathList)
        {
            bool lSuccess = true;

            foreach (string lPath in pScriptPathList)
            {
                try
                {
                    FileInfo lPatchFile = new FileInfo(lPath);
                    if (!Path.IsPathRooted(lPath))
                    {
                        //If not absolute, evaluate from the base directory
                        lPatchFile = new FileInfo(Path.Combine(pBaseDirectory.FullName, lPath));
                    }

                    Console.WriteLine($"\n********** {lPatchFile.Name} **********\n");

                    string lFileContents = File.ReadAllText(lPatchFile.FullName);
                    PatchScript lPatchScript = createFromString(lPatchFile.FullName, lFileContents);

                    Console.WriteLine($"Patch label: {lPatchScript.PatchLabel}");
                    Console.WriteLine($"Patch number: {lPatchScript.PatchNumber}");
                    Console.WriteLine($"Patch description: {lPatchScript.Description}");

                    foreach (ScriptExecutable lExec in lPatchScript.ExecutableList)
                    {
                        Console.WriteLine(PRINT_STATEMENT_DIVIDER);
                        Console.WriteLine(lExec.getDisplayString());
                    }

                    Console.WriteLine(PRINT_STATEMENT_DIVIDER);

                }
                catch (IOException e)
                {
                    Console.WriteLine("ERROR: Could not read PatchScript file");
                    Console.WriteLine($"Reason (see log for details): {e.Message}");
                    Logger.logError(e);
                    lSuccess = false;
                }
                catch (ExParser e)
                {
                    Console.WriteLine("ERROR: PATCHSCRIPT COULD NOT BE PARSED");
                    Console.WriteLine($"Reason (see log for details): {e.Message}");
                    Logger.logError(e);
                    lSuccess = false;
                }
            }

            return lSuccess;
        }

        /**
         * Constructs a new PatchScript by parsing the given file contents.
         * @param pFileName File name of the PatchScript.
         * @param pPatchContents File contents.
         * @return The new PatchScript.
         * @throws ExParser If the file contents or file name cannot be parsed.
         */
        public static PatchScript createFromString(String pFileName, String pPatchContents)
        {
            return createFromString(pFileName, pPatchContents, "unavailable", "unavailable");
        }

        /**
         * Constructs a new PatchScript by parsing the given file contents.
         * @param pFileName File name of the PatchScript.
         * @param pPatchContents File contents.
         * @param pFileHash File hash of the patch script.
         * @param pFileVersion Version of the patch script.
         * @return The new PatchScript.
         * @throws ExParser If the file contents or file name cannot be parsed.
         */
        public static PatchScript createFromString(String pFileName, String pPatchContents, String pFileHash, String pFileVersion)
        {
            return new PatchScript(pFileName, pPatchContents, pFileHash, 0, pFileVersion);
        }

        /**
         * Constructs a new PatchScript by reading the contents of a PromotionFile.
         * @param pResolver Resolver for finding the file.
         * @param pPromotionFile File to be parsed.
         * @return The new PatchScript.
         * @throws IOException If the file cannot be read.
         * @throws ExParser If the file contents or file name cannot be parsed.
         */
        public static PatchScript createFromPromotionFile(FileResolver pResolver, PromotionFile pPromotionFile)
        {
            FileInfo lFile = pResolver.resolveFile(pPromotionFile.getFilePath());
            String lFileContents = FileUtils.readFileToString(lFile);
            return new PatchScript(lFile.getName(), lFileContents, pPromotionFile.getFileHash(), pPromotionFile.getSequencePosition(), pPromotionFile.getFileVersion());
        }

        /**
         * Constructs a new PatchScript.
         * @param pFileName Patch file name.
         * @param pFileContents Contents of the file.
         * @param pPatchFileHash Hash of the file.
         * @param pPromotionSequencePosition Position within the overall promotion.
         * @param pFileVersion VCS version of the file.
         * @throws ExParser If the contents or filename cannot be parsed.
         */
        private PatchScript(String pFileName, String pFileContents, String pPatchFileHash, int pPromotionSequencePosition, String pFileVersion)
  throws ExParser
        {

            //Use regex to split the filename into its component parts
            Matcher lMatcher = FILENAME_PATTERN.matcher(pFileName);
    if(lMatcher.matches()){
                mPatchLabel = lMatcher.group(FILENAME_PATTERN_TYPE_GROUP);
                mPatchNumber = Integer.parseInt(lMatcher.group(FILENAME_PATTERN_NUMBER_GROUP));
                mDescription = lMatcher.group(FILENAME_PATTERN_DESCRIPTION_GROUP);

                Logger.logDebug("Parsed patch filename " + pFileName + ": Patch Label = " + mPatchLabel + " Number = " + mPatchNumber + " Description = " + mDescription);
            }
    else {
                throw new ExParser("Invalid patch filename '" + pFileName + "'. Expected format is 'PATCHLABEL##### (description).sql'");
            }

            //Split the nested scripts into individual executable scripts
            mExecutableList = ScriptExecutableParser.parseScriptExecutables(pFileContents, false);

            mPatchFileHash = pPatchFileHash;
            mPromotionSequencePosition = pPromotionSequencePosition;
            mOriginalPatchString = pFileContents;
            mFileVersion = pFileVersion;
        }

        /**
         * Gets the original contents of the file used to create this PatchScript, before it was parsed.
         * @return Original file contents.
         */
        public String getOriginalPatchString()
        {
            return mOriginalPatchString;
        }

        /**
         * Gets this PatchScript's list of ScriptExecutables.
         * @return Executable list.
         */
        public List<ScriptExecutable> getExecutableList()
        {
            return mExecutableList;
        }

        /**
         * Gets the unique display name of this PatchScript. This is the label concatenated with the number.
         * @return Display name.
         */
        public String getDisplayName()
        {
            return mPatchLabel + " " + mPatchNumber;
        }

        /**
         * Gets the patch label, e.g. PATCHCORE, POSTPATCHCORE, etc.
         * @return Patch label.
         */
        public String getPatchLabel()
        {
            return mPatchLabel;
        }

        /**
         * Gets the number sequence of this PatchScript.
         * @return Patch number.
         */
        public int getPatchNumber()
        {
            return mPatchNumber;
        }

        /**
         * Gets the position of this PatchScript within its overall promotion label.
         * @return Promotion position.
         */
        public int getPromotionSequencePosition()
        {
            return mPromotionSequencePosition;
        }

        /**
         * Gets the file hash of this patch's original file.
         * @return File hash.
         */
        public String getPatchFileHash()
        {
            return mPatchFileHash;
        }

        /**
         * Gets the description of this PatchScript as specified in the parenthesised part of the file name.
         * @return Patch description.
         */
        public String getDescription()
        {
            return mDescription;
        }

        /**
         * Gets the VCS version string for the file which created this PatchScript.
         * @return Version number.
         */
        public String getFileVersion()
        {
            return mFileVersion;
        }
    }
}