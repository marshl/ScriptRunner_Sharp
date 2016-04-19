using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ScriptRunner_Sharp
{
    /**
     * Representation of a line in a Manifest or Manifest override file. A ManifestEntry mainly comprises four parts:
     *
     * <ol>
     * <li>A sequence position determining the order of the promotion</li>
     * <li>A loader name, referencing the {@link com.fivium.scriptrunner2.loader.Loader Loader} which will be used to promote the file</li>
     * <li>A relative file path for resolving the actual file to be promoted</li>
     * <li>A property map containing properties which can be bound in to the loader or used to verify the integrity of the file</li>
     * </ol>
     *
     * A ManifestEntry may be a PromotionFile, which is used during a promotion, or a BuilderManifestEntry, which is used
     * when building a manifest. BuilderManifestEntries may be "partial" entries of which there are two types:
     *
     * <ol>
     * <li>Forced duplicates (+ prefix)</li>
     * <li>Augmentations (~ prefix)</li>
     * </ol>
     *
     * Forced duplicate entries are used to force a file to be included more than once in a manifest. Augmentation entries
     * allow users to specify additional properties or alternative sequence positions in manifest override files, to override
     * the default behavior of the ManifestBuilder.
     */
    public abstract class ManifestEntry
    {


        private static readonly string LOADER_NAME_REGEX = "[A-Za-z0-9_\\-\\.]"; //Loader names may only contain alphanumerics, underscores or hyphens
        public static readonly string FILE_PATH_REGEX = "[A-Za-z0-9_\\-.\\(\\)\\\\/\\[\\],;'#\\{\\}\\[\\]@\\+\\$�\\!\\^% ]"; //Note that spaces are allowed in a file path  
        private static readonly Regex MANIFEST_ENTRY_PATTERN = new Regex("^(([0-9]+):|~)[ \\t]+(?:(" + LOADER_NAME_REGEX + "+)[ \\t]+)?(\\+?)(" + FILE_PATH_REGEX + "+)(?:[ \\t]+(\\{.*\\}))?[ \\t]*$", RegexOptions.Compiled);

        private readonly bool mIsAugmentation;
        private readonly string mFilePath;
        private readonly string mLoaderName;
        private readonly Dictionary<string, string> mPropertyMap;
        private readonly bool mIsForcedDuplicate;

        private string mFileHash = null;

        private int mFileIndex;

        private static readonly HashAlgorithm gHashFunction = MD5.Create();

        /**
         * Parses a manifest file line into a BuilderManifestEntry or PromotionFile.
         * @param pLine String contents of the line.
         * @param pIsPromotionFile If true, the ManifestEntry returned will be a PromotionFile. Otherwise it will be a BuilderManifestEntry.
         * @return The parsed representation of the line.
         * @throws ExParser If the line cannot be parsed.
         */
        public static ManifestEntry parseManifestFileLine(string pLine, bool pIsPromotionFile)
        {
            Match lMatcher = MANIFEST_ENTRY_PATTERN.Match(pLine);

            if (lMatcher.Success)
            {

                bool lIsAugmentation = lMatcher.Groups[1].Value == "~";
                string lSequencePositionString = lMatcher.Groups[2].Value;
                string lLoaderName = lMatcher.Groups[3].Value ?? string.Empty;
                bool lForce = lMatcher.Groups[4].Value == "+";
                string lFilePath = lMatcher.Groups[5].Value.Trim();
                string lPropertiesString = lMatcher.Groups[6].Value;

                //Trim an optional leading slash from the start of the file name
                if (lFilePath[0] == '/' || lFilePath[0] == '\\')
                {
                    lFilePath = lFilePath.Substring(1);
                }

                Dictionary<string, string> lPropertyMap;
                if (lPropertiesString != null && !string.IsNullOrWhiteSpace(lPropertiesString))
                {
                    lPropertyMap = ManifestParser.parsePropertyMapString(lPropertiesString);
                }
                else
                {
                    lPropertyMap = new TreeMap<String, String>();
                }

                //Perform some validation

                if (pIsPromotionFile && lIsAugmentation)
                {
                    //Promotion files MUST have a sequence
                    throw new ExParser("Manifest entries for promotions cannot specify augmentation (~) lines. Offending line:\n" + pLine);
                }

                if (pIsPromotionFile && "".equals(lLoaderName.trim()))
                {
                    //Promotion files MUST have a loader name
                    throw new ExParser("Manifest entries for promotions must specify loader names. Offending line:\n" + pLine);
                }
                else if (!pIsPromotionFile && !lIsAugmentation && "".equals(lLoaderName.trim()))
                {
                    //Override files MUST have a loader name
                    throw new ExParser("Non-augmentation (~) manifest entries for promotions must specify loader names. Offending line:\n" + pLine);
                }

                int lSequencePosition = -1;
                if (!lIsAugmentation)
                {
                    try
                    {
                        lSequencePosition = Integer.parseInt(lSequencePositionString);
                    }
                    catch (NumberFormatException e)
                    {
                        throw new ExParser("Promotion file syntax: sequence position '" + lSequencePositionString + "' is not a valid integer", e);
                    }
                }

                if (pIsPromotionFile)
                {
                    return new PromotionFile(lFilePath, lLoaderName, lSequencePosition, lPropertyMap, lForce);
                }
                else
                {
                    return new BuilderManifestEntry(lIsAugmentation, lFilePath, lLoaderName, lSequencePosition, lPropertyMap, lForce);
                }
            }
            else
            {
                throw new ExParser("Promotion file syntax not valid: " + pLine);
            }
        }

        public ManifestEntry(boolean pIsAugmentation, String pFilePath, String pLoaderName, Map<String, String> pPropertyMap, boolean pIsForcedDuplicate)
        {
            mIsAugmentation = pIsAugmentation;
            mFilePath = ScriptRunner.normaliseFilePath(pFilePath);
            mLoaderName = pLoaderName.trim();
            mPropertyMap = pPropertyMap;
            mIsForcedDuplicate = pIsForcedDuplicate;
        }

        /**
         * Gets the position of this entry in the overal promotion order.
         * @return Sequence position.
         */
        public abstract int getSequencePosition();

        /**
         * Get the normalised file path for this entry, relativised to the manifest base directory.
         * @return File path.
         */
        public String getFilePath()
        {
            return mFilePath;
        }

        /**
         * Gets the name of the loader which will be used to promote the file implicated by this ManifestEntry.
         * @return Loader name.
         */
        public String getLoaderName()
        {
            return mLoaderName;
        }

        /**
         * Gets the property map for this ManifestEntry.
         * @return Property map.
         */
        public Map<String, String> getPropertyMap()
        {
            return mPropertyMap;
        }

        /**
         * Tests if this entry is a forced duplicate (i.e. a "+"-prefixed entry which is used in a manifest override to force
         * a file to be promoted more than once).
         * @return True if this is a forced duplicate.
         */
        public boolean isForcedDuplicate()
        {
            return mIsForcedDuplicate;
        }

        /**
         * Gets the hash of this file, previously generated by calling {@link #generateFileHash}.
         * @return The file hash, or null if it has not been generated.
         */
        public String getFileHash()
        {
            return mFileHash;
        }

        /**
         * Hashes a the file at the given path using the standard hash function.
         * @param pFilePath Path of file to hash.
         * @param pResolver Resolver for resolving file path.
         * @return The hash of the file.
         * @throws IOException If the file cannot be read.
         */
        public static String hashFile(String pFilePath, FileResolver pResolver)
      throws IOException
        {    
    return Files.hash(pResolver.resolveFile(pFilePath), gHashFunction).toString();
        }

        /**
         * Generates the hash for this file, if it is not already generated, and sets the appropriate member variable for
         * future retrieval.
         * @param pResolver Resolver for locating the file.
         * @return The hash of the file.
         * @throws IOException If the file cannot be read.
         */
        public String generateFileHash(FileResolver pResolver)
      throws IOException
        {
    if(mFileHash == null){
                mFileHash = hashFile(mFilePath, pResolver);
            }
    return mFileHash;
        }

        /**
         * Tests if this entry is an augmentation entry (i.e. a "~"-prefixed entry used for overriding the position or properties
         * of the same file's corresponding default entry).
         * @return True if this entry is an augmentation.
         */
        public boolean isAugmentation()
        {
            return mIsAugmentation;
        }

        /**
         * Sets the 1-based index of this file within the manfiest, relative to other files of the same name.
         * @param pFileIndex File index.
         */
        public void setFileIndex(int pFileIndex)
        {
            mFileIndex = pFileIndex;
        }

        /**
         * Gets the 1-based index of this file within the manifest, in relation to other files with the same name. I.e. "MyFile.txt"
         * at position 1000 would have index 1, "MyFile.txt" at position 1500 would have index 2, etc.
         * @return File index.
         */
        public int getFileIndex()
        {
            return mFileIndex;
        }
    }

}