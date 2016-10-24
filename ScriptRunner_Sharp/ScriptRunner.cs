

namespace ScriptRunner_Sharp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /**
* A ScriptRunner is used to promote files to a database, following the rules defined in a mandatory manifest file.
* The operation is coordinated by the static <tt>RUN</tt> method so there is no need to instantiate a ScriptRunner
* directly.
*/
    public class ScriptRunner : FileResolver
    {

        /** Name of the directory in the top level of a deployment archive where ScriptRunner configuration is located */
        public const string SCRIPTRUNNER_DIRECTORY_NAME = "ScriptRunner";

        /** Relative path to the manifest file in a deployment archive */
        public const string MANIFEST_RELATIVE_FILE_PATH = SCRIPTRUNNER_DIRECTORY_NAME + "/manifest.mf";

        /** Base directory of the promote. */
        private readonly DirectoryInfo mBaseDirectory;

        /** Flag for storing if the base directory is a temporary directory (created by extracting an archive) */
        private readonly bool mIsBaseDirectoryTemp;

        /** The connections to be used for the promote. */
        private DatabaseConnection mDatabaseConnection = null;

        /** The controller to be used for the promote. */
        private PromotionController mPromotionController = null;

        /** Container for all command line options which were used to invoke ScriptRunner. */
        private readonly CommandLineWrapper mCommandLineWrapper;

        /** List of log entries which is populated when ScriptRunner is run in -noexec mode */
        private readonly List<NoExecLogEntry> mNoExecLog = new List<NoExecLogEntry>();

        /**
         * Entry point for performing a ScriptRunner promotion.
         * @param pCommandLineWrapper Command line object containing all applicable options for this run.
         */
        public static void run(CommandLineWrapper pCommandLineWrapper)
        {
            //Construct a new instance to perform the promote
            ScriptRunner lScriptRunner = new ScriptRunner(pCommandLineWrapper);
            bool lSuccess = false;
            try
            {
                lSuccess = lScriptRunner.doPromote();
            }
            finally
            {
                lScriptRunner.cleanUp();
                if (lSuccess)
                {
                    lScriptRunner.logNoExecResults();
                }
            }
        }

        /**
         * Convenience method for normalising a file path throughout ScriptRunner. All path Strings should use Unix path
         * seperators. This is only required when paths are to be compared as strings; the JDK File class should handle OS 
         * differences between file paths.
         * @param pPath File path to normalise.
         * @return Normalised file path.
         */
        public static string normaliseFilePath(string pPath)
        {
            return FilenameUtils.separatorsToUnix(pPath);
        }

        /**
         * Constructs a new ScriptRunner object to be used to promote files from the source location, specified in the OptionWrapper.
         * This can be a directory or a zip archive. If it is a zip it will be extracted to a temporary directory.
         * @param pCommandLineWrapper Container for all command line arguments.
         */
        private ScriptRunner(CommandLineWrapper pCommandLineWrapper)
        {

            mCommandLineWrapper = pCommandLineWrapper;
            string lSourceLocation = mCommandLineWrapper.RunDirectory;

            //Validate source location is not null
            if (string.IsNullOrEmpty(lSourceLocation))
            {
                throw new ExFatalError($"-run argument must be specified");
            }

            FileInfo lSourceFile = new FileInfo(lSourceLocation);
            if (!lSourceFile.Exists)
            {
                throw new ExFatalError($"Failed to locate source file at {lSourceLocation}");
            }

            DirectoryInfo sourceFileDirectory = new DirectoryInfo(lSourceLocation);
            if (sourceFileDirectory.Exists)
            {
                mIsBaseDirectoryTemp = false;
                mBaseDirectory = sourceFileDirectory;
            }
            else
            {
                mIsBaseDirectoryTemp = true;
                string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                mBaseDirectory = Directory.CreateDirectory(tempDirectory);
                try
                {
                    Logger.logInfo("Extracting archive " + lSourceLocation);
                    int lFileCount = ArchiveUtil.extractZipToFolder(lSourceFile, mBaseDirectory);
                    Logger.logInfo("Extracted " + lFileCount + " files");
                }
                catch (ZipException e)
                {
                    throw new ExInternal($"Zip error extracting zip to {mBaseDirectory.FullName}", e);
                }
                catch (IOException e)
                {
                    throw new ExInternal($"IO error extracting zip to {mBaseDirectory.FullName}", e);
                }
            }

            Logger.logInfo($"Base directory is {mBaseDirectory.FullName}");
        }

        /**
         * Gets a file from this ScriptRunner's base directory.
         * @param pPath A path to the desired file, relative to the base directory.
         * @return The requested file.
         * @throws FileNotFoundException If the file does not exist.
         */
        public FileInfo resolveFile(string pPath)
        {
            FileInfo lFile = new FileInfo(Path.Combine(mBaseDirectory.FullName, pPath));

            if (!lFile.Exists)
            {
                throw new FileNotFoundException("Failed to locate file " + pPath + " in base directory");
            }

            return lFile;
        }

        /**
         * Gets the path of the given file relativised to the current base directory and normalised.
         * @param pFile File to get path of.
         * @return Relativised file path.
         */
        public string relativeFilePath(FileInfo pFile)
        {
            Uri baseUri = new Uri(mBaseDirectory.FullName);
            Uri fileUri = new Uri(pFile.FullName);
            return baseUri.MakeRelativeUri(fileUri).LocalPath;
        }

        /**
         * Gets a string set of all the paths of all the files in the given base directory, excluding the /ScriptRunner
         * leg. Paths are relativised to the base directory and path seperators
         * are normalised.
         * @param pFileResolver Resolver for relativising file paths.
         * @return Set of relativised file paths.
         */
        public static HashSet<string> allFilePathsInBaseDirectory(FileResolver pFileResolver)
        {
            HashSet<string> lPathSet = new HashSet<string>();
            //Recursively get all the files in the base directory (exclude the ScriptRunner directory)
            List<FileInfo> lFileList = pFileResolver.getBaseDirectory().GetFiles(".*").Where(x => x.Name != "ScriptRunner").ToList();

            //Relativize each path to the base directory and add to set
            foreach (FileInfo lFile in lFileList)
            {
                lPathSet.Add(normaliseFilePath(pFileResolver.relativeFilePath(lFile)));
            }

            return lPathSet;
        }

        /**
         * Gets a string set of all the paths of all the files in the current base directory, excluding the /ScriptRunner
         * leg which will always contain only metadata. Paths are relativised to the base directory and path seperators
         * are normalised.
         * @return Set of relativised file paths.
         */
        public HashSet<string> allFilePathsInBaseDirectory()
        {
            return allFilePathsInBaseDirectory(this);
        }

        /**
         * Locates the manifest file in the base directory, parses it and validates its contents. Any exceptions at this
         * stage will cause the promote to fail.
         * @return A ManifestParser which has been parsed and verified, and is ready to be used for a promotion.
         * @throws ExFatalError If the manifest cannot be loaded or verified.
         */
        private PromotionManifestParser loadManifest()
        {
            //Locate the manifest file in the base directory
            FileInfo lManifestFile;
            try
            {
                lManifestFile = resolveFile(MANIFEST_RELATIVE_FILE_PATH);
            }
            catch (FileNotFoundException e)
            {
                throw new ExFatalError("Cannot locate manifest file", e);
            }

            //Parse the manifest
            PromotionManifestParser lParser = new PromotionManifestParser(lManifestFile);
            try
            {
                lParser.parse();
            }
            catch (FileNotFoundException e)
            {
                throw new ExFatalError("Failed to parse manifest: file not found", e);
            }
            catch (IOException e)
            {
                throw new ExFatalError("Failed to parse manifest: IOException", e);
            }
            catch (ExParser e)
            {
                throw new ExFatalError("Failed to parse manifest: " + e.Message, e);
            }
            catch (ExManifest e)
            {
                throw new ExFatalError("Failed to load manifest: " + e.Message, e);
            }

            //Verify the manifest
            try
            {
                Logger.logInfo("Verifiying manifest...");
                lParser.verifyManifest(this);
            }
            catch (ExManifest e)
            {
                throw new ExFatalError("Manifest verification failed: " + e.Message, e);
            }

            Logger.logInfo("Manifest parsed and verified successfully");

            return lParser;
        }

        /**
         * Pre-parses all patch scripts in this promote, and returns a map of file paths to PatchScripts.
         * This should be performed in advance of the promote to catch any parsing issues before runtime. This method also
         * verifies that the scripts will be executed in the correct order.
         * @param pManifestParser ManifestParse containing all promotion files.
         * @return Map of file paths to PatchScripts.
         * @throws ExFatalError If there is an ordering problem or parsing problem.
         */
        private Dictionary<string, PatchScript> preParsePatchScripts(PromotionManifestParser pManifestParser)
        {

            Logger.logInfo("Validating patches...");

            Dictionary<string, PatchScript> lParsedScriptMap = new Dictionary<string, PatchScript>();
            //Map of patch labels to highest orders - used to verify script order is correct
            Dictionary<string, int?> lScriptNumberMap = new Dictionary<string, int?>();

            foreach (PromotionFile lPromotionFile in pManifestParser.getPromotionFileList())
            {
                if (BuiltInLoader.LOADER_NAME_PATCH == lPromotionFile.getLoaderName())
                {
                    try
                    {
                        PatchScript lScript = PatchScript.createFromPromotionFile(this, lPromotionFile);
                        int? lPreviousNumber = lScriptNumberMap[lScript.getPatchLabel()];

                        if (lPreviousNumber != null && lScript.getPatchNumber() < lPreviousNumber)
                        {
                            throw new ExFatalError("Patch order violation - " + lScript.getDisplayName() + " is implicated after patch with number " + lPreviousNumber);
                        }

                        if (lPromotionFile.isForcedDuplicate())
                        {
                            throw new ExFatalError("Patch " + lScript.getDisplayName() + " at position " + lPromotionFile.getSequencePosition() +
                                                   " cannot be a forced duplicate; patches may only be run once in a promote.");
                        };

                        lParsedScriptMap.Add(lPromotionFile.getFilePath(), lScript);
                        lScriptNumberMap.Add(lScript.getPatchLabel(), lScript.getPatchNumber());
                    }
                    catch (ExParser e)
                    {
                        throw new ExFatalError("Could not parse patch " + lPromotionFile.getFilePath() + ": " + e.Message, e);
                    }
                    catch (IOException e)
                    {
                        throw new ExFatalError("Could not parse patch " + lPromotionFile.getFilePath(), e);
                    }
                }
            }

            return lParsedScriptMap;

        }

        /**
         * Create a PromotionController for this ScriptRunner invocation. If the -noexec argument has been used, the controller
         * will not allow the promotion of any files.
         * @param pPromotionLabel Promotion label of the current promotion.
         * @return A new PromotionController.
         */
        private PromotionController createPromotionController(String pPromotionLabel)
        {
            if ( mCommandLineWrapper.SkipExecution)
            {
                return new PromotionController(getDatabaseConnection(), pPromotionLabel);
            }
            else
            {
                return new NoExecPromotionController(this, getDatabaseConnection(), pPromotionLabel);
            }

        }

        /**
         * Create a PatchRunController for running a PatchScript.
         * @param pForPatchScript PatchScript to be run.
         * @return A new PatchRunController.
         */
        public PatchRunController createPatchRunController(PatchScript pForPatchScript)
        {
            if (!hasCommandLineOption(CommandLineOption.NO_EXEC))
            {
                return new PatchRunController(this, pForPatchScript);
            }
            else
            {
                return new NoExecPatchRunController(this, pForPatchScript);
            }
        }

        /**
         * Parses the manifest, parses and validates all loaders, establishes a database connection and then runs the promotion.
         * @throws Throwable In the event of any error.
         */
        private boolean doPromote()
  throws Throwable
        {

            Logger.logAndEcho("Starting " +  (hasCommandLineOption(CommandLineOption.NO_EXEC) ? "-noexec " : "") + "promotion");
            Logger.logAndEcho("Parsing files...");

            //Parse and verify the manifest
            PromotionManifestParser lManifestParser;
            Map<String, Loader> lLoaderMap;
            Map<String, PatchScript> lParsedScriptMap;
    try {
                lManifestParser = loadManifest();

                //Construct a definitive single map of loaders to be used by this promote by combining loaders from the manifest
                //with built in loaders.
                lLoaderMap = new HashMap<String, Loader>(lManifestParser.getLoaderMap());
                lLoaderMap.putAll(BuiltInLoader.getBuiltInLoaderMap());

                //Parse all implicated scripts in advance to check for syntax errors - this will error out if there is a problem
                lParsedScriptMap = preParsePatchScripts(lManifestParser);

                Logger.logInfo("Validating loaders...");

                //Prepare all the PL/SQL loaders in advance
                for (MetadataLoader lLoader : lManifestParser.getLoaderMap().values())
                {
                    lLoader.prepare(this);
                }

                //Check all bind variables that each loader requires can be provided by each PromotionFile
                for (PromotionFile lFile : lManifestParser.getPromotionFileList())
                {
                    Loader lLoader = lLoaderMap.get(lFile.getLoaderName());
                    if (lLoader instanceof MetadataLoader){
                    try
                    {
                        ((MetadataLoader)lLoader).validateForFile(this, lFile);
                    }
                    catch (ExPromote e)
                    {
                        throw new ExFatalError("Loader validation failed: " + e.Message, e);
                    }
                }
            }

            //Establish a connection to the target database
            mDatabaseConnection = DatabaseConnection.createConnection(mCommandLineWrapper);

            //Create a new promotion controller for interfacing with the database log tables
            mPromotionController = createPromotionController(lManifestParser.getPromotionPropertyMap().get(ManifestParser.PROMOTION_LABEL_PROPERTY));

            }
    catch(Throwable th){
                //Log the stacktrack of any errors up to this point
                Logger.logError(th);
                throw th;
            }

            //*****************
            //Start promote
            //*****************

            boolean lSuccess = true;
            ExFatalError lError = null;
    try {

                boolean lStartAllowed = mPromotionController.startPromote();

                if (lStartAllowed)
                {
                    //Promote all files
                    for (PromotionFile lFile : lManifestParser.getPromotionFileList())
                    {
                        Loader lLoader = lLoaderMap.get(lFile.getLoaderName());
                        if (lLoader instanceof PatchScriptLoader){
                        //If this is a patch, directly load the pre-parsed patch
                        ((PatchScriptLoader)lLoader).runPatchScript(this, lParsedScriptMap.get(lFile.getFilePath()));
                    }
          else {
                        //For all other file types, load as normal
                        lLoader.promoteFile(this, lFile);
                    }
                }
            }
        }
    catch(ExPromote e){      
      //Message should contain enough context, just re-raise      
      lSuccess = false;
      lError = new ExFatalError("Promotion failed: " + e.Message, e);     
      throw lError;
    }
    catch(Throwable th){
      lSuccess = false;
      lError = new ExFatalError("Unexpected error: " + th.Message, th);
      throw lError;
    }
    finally {
      
      //Write any error to all logs
      if(lError != null){
        Logger.logInfo("\n\nSERIOUS ERROR while executing ScriptRunner! Marking promotion as failed.\nSee below for error details:\n");
        Logger.logError(lError);
      }
      
      //Set the status of the promotion run to COMPLETE or FAILED, depending on if there was an error
      try {
        mPromotionController.endPromote(lError == null);
      }
      catch(Throwable th){
        //Do not allow logging exceptions to take precedence over the original error if there was one
        if(lError != null){
          Logger.logInfo("Error encountered when finalising log row:\n" + th.Message);
        }
        else {
          //If the promote succeeded but logging failed, tell the user
          throw new ExFatalError("Error encountered when finalising log row", th);
        }        
      }      
    }

    //Finalise the promotion connection
    try {
      mDatabaseConnection.closePromoteConnection();
    }
    catch (Throwable th){
      //Suppress errors caused by closing the connection
      Logger.logWarning("Failed to close connection: " + th.Message);
    }
    
    return lSuccess;
  }
  
  /**
   * Formats and outputs the list of NoExecLogEntries to all loggers.
   */
  private void logNoExecResults()
{
    if (hasCommandLineOption(CommandLineOption.NO_EXEC))
    {

        final String lSequenceHeader = "Sequence";
        final String lNameHeader = "Description";
        final String lDetailsHeader = "Details";
        int lLongestSequence = lSequenceHeader.length();
        int lLongestName = lNameHeader.length();
        int lLongestDetails = lDetailsHeader.length();
        final int lResultLength = 10;

        //Establish the lengths of the longest entries
        for (NoExecLogEntry lLogEntry : mNoExecLog)
        {
            if (lLongestSequence < Integer.toString(lLogEntry.mSequence).length())
            {
                lLongestSequence = Integer.toString(lLogEntry.mSequence).length();
            }
            if (lLongestName < lLogEntry.mDisplayName.length())
            {
                lLongestName = lLogEntry.mDisplayName.length();
            }
            if (lLongestDetails < lLogEntry.mDetails.length())
            {
                lLongestDetails = lLogEntry.mDetails.length();
            }
        }
        lLongestName += 2;

        Logger.logInfo("\nNoExec Result\n====================================\n");

        String lFormatMask = "%-" + lLongestName + "s%-" + lResultLength + "s%s";

        String lHeaderFormatMask = "%" + (lLongestSequence) + "s  " + lFormatMask;
        String lRowFormatMask = "%0" + (lLongestSequence) + "d  " + lFormatMask;

        //Output the title row
        Logger.logInfoFormatted(lHeaderFormatMask, lSequenceHeader, lNameHeader, "Result", lDetailsHeader);

        //Draw a divider to seperate the title row from the output rows
        String lDivider = "";
        for (int i = 0; i < lLongestName + lResultLength + lLongestDetails + lLongestSequence + 2; i++)
        {
            lDivider += "-";
        }
        Logger.logInfo(lDivider);

        for (NoExecLogEntry lLogEntry : mNoExecLog)
        {
            Logger.logInfoFormatted(lRowFormatMask, lLogEntry.mSequence, lLogEntry.mDisplayName, lLogEntry.mWillPromote ? "PROMOTE" : "SKIP", lLogEntry.mDetails);
        }

        Logger.logInfo("\nEnd NoExec Result\n");
    }
}

/**
 * Cleans up temporary resources after a promotion is complete.
 */
private void cleanUp()
{
    if (mIsBaseDirectoryTemp)
    {
        Logger.logDebug("Removing temporary directory");
        try
        {
            FileUtils.deleteDirectory(mBaseDirectory);
        }
        catch (IOException e)
        {
            Logger.logDebug("Failed when removing temporary directory: " + e.Message);
        }
    }
}

/**
 * Gets this ScriptRunner's current database connection.
 * @return Database connection.
 */
public DatabaseConnection getDatabaseConnection()
{
    return mDatabaseConnection;
}

/**
 * Gets this ScriptRunner's current promotion controller.
 * @return Promotion controller.
 */
public PromotionController getPromotionController()
{
    return mPromotionController;
}

/**
 * Gets the value of the given command line option which was used when invoking ScriptRunner.
 * @param pCommandLineOption Option to get value for.
 * @return Option value.
 */
public String getCommandLineOption(CommandLineOption pCommandLineOption)
{
    return mCommandLineWrapper.getOption(pCommandLineOption);
}

/**
 * Tests if the given command line option was specified when invoking ScriptRunner.
 * @param pCommandLineOption Option to test.
 * @return True if the option was specified.
 */
public boolean hasCommandLineOption(CommandLineOption pCommandLineOption)
{
    return mCommandLineWrapper.hasOption(pCommandLineOption);
}

@Override
  public File getBaseDirectory()
{
    return mBaseDirectory;
}

/**
 * Adds an entry to the noexec log for a promotion label.
 * @param pLabel Promotion label.
 * @param pWillPromote True if this label will be promoted.
 * @param pDetails Extra details.
 */
public void addNoExecLabelLog(String pLabel, boolean pWillPromote, String pDetails)
{
    mNoExecLog.add(new NoExecLogEntry("Promotion " + pLabel, 0, pWillPromote, pDetails));
}

/**
 * Adds an entry to the noexec log for a promotion file.
 * @param pPromotionFile Promotion file being promoted.
 * @param pWillPromote True if this file will be promoted.
 * @param pDetails Extra details.
 */
public void addNoExecFileLog(PromotionFile pPromotionFile, boolean pWillPromote, String pDetails)
{
    mNoExecLog.add(new NoExecLogEntry(pPromotionFile.getFilePath(), pPromotionFile.getSequencePosition(), pWillPromote, pDetails));
}

/**
 * Adds an entry to the noexec log for a PatchScript.
 * @param pPatchScript PatchScript being run.
 * @param pWillPromote True if this file will be promoted.
 * @param pDetails Extra details.
 */
public void addNoExecPatchLog(PatchScript pPatchScript, boolean pWillPromote, String pDetails)
{
    mNoExecLog.add(new NoExecLogEntry(pPatchScript.getDisplayName(), pPatchScript.getPromotionSequencePosition(), pWillPromote, pDetails));
}

/**
 * Adds an entry to the noexec log for a statement within a PatchScript.
 * @param pStatement PatchScript statement.
 * @param pWillRun True if this statement will be run.
 */
public void addNoExecPatchStatementLog(ScriptSQL pStatement, boolean pWillRun)
{
    mNoExecLog.add(new NoExecLogEntry("  Statement " + pStatement.getScriptIndex(), pStatement.getScriptIndex(), pWillRun, pStatement.getStatementPreview()));
}

/**
 * Data class for storing rows in the noexec log.
 */
private static class NoExecLogEntry
{
    final String mDisplayName;
    int mSequence;
    final boolean mWillPromote;
    final String mDetails;    

    NoExecLogEntry(String pDisplayName, int pSequence, boolean pWillPromote, String pDetails)
    {
        mDisplayName = pDisplayName;
        mSequence = pSequence;
        mWillPromote = pWillPromote;
        mDetails = pDetails;
    }
}
}
