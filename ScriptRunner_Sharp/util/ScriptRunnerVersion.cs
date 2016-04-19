using System.Collections.Generic;

namespace ScriptRunner_Sharp
{
    /**
     * Utility class for determining the version of ScriptRunner currently in use.
     */
    public class ScriptRunnerVersion
    {
        private ScriptRunnerVersion() { }

        private const string VERSION_FILE_NAME = "version.properties";
        private const string VERSION_PROPERTY = "version_number";
        private const string SOFTWARE_NAME_PROPERTY = "software_name";


        /**
         * Gets the current version number of ScriptRunner as specified in the version file.  This file should be maintained 
         * by the build process (i.e. Ant).
         * @return Current ScriptRunner version number as a string.
         */
        public static string getVersionNumber()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        /**
         * Get the full version string, i.e. the current software name concatenated with the current version number,
         * as defined in the version properties file.
         * @return Version string.
         */
        public static String getVersionString()
        {
            return getOrInitProperties().getProperty(SOFTWARE_NAME_PROPERTY, "ScriptRunner") + " version " + getVersionNumber();
        }

        /**
         * Gets the latest update patch number (i.e. the latest PATCHSCRIPTRUNNER patch) which this version of ScriptRunner
         * is expecting to have been run. This will be checked against the target database. This number should reflect the 
         * latest update patch in the update package.
         * @return Patch number of the latest ScriptRunner install patch which this version is expecting.
         */
        public static int getLatestExpectedUpdatePatchNumber()
        {
            List<PatchScript> lList = Updater.getUpdatePatches();
            return lList[lList.Count - 1].getPatchNumber();
        }

        public static int getLatestUpdatePatchNumber(Connection pConnection)
        {
            try
            {
                return SQLManager.queryScalarInt(pConnection, SQLManager.SQL_FILE_VERSION_CHECK, Installer.INSTALL_PATCH_PREFIX);
            }
            catch (SQLException e)
            {
                throw new ExFatalError("Error getting latest update patch number: " + e.Message, e);
            }
        }

    }

}