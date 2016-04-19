using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System;

namespace ScriptRunner_Sharp
{
    /**
     * Parser for parsing either a Manifest file, Manifest override file, or additional properties file. This class provides
     * functionality common to all three use cases. The {@link PromotionManifestParser} subclass provides additional
     * validation for parsing a Manifest file which will be used to perform a promotion.
     */
    public class ManifestParser
    {

        /**
         * Pattern to match a PROMOTION instruction.
         * Line syntax: "PROMOTION PromotionName"
         */
        private static readonly Regex MANIFEST_LINE_PROMOTION_PROPERTIES_PATTERN = new Regex("^PROMOTION[ \\t]+(\\{.+\\})$", RegexOptions.Compiled);

        public const string PROMOTION_LABEL_PROPERTY = "promotion_label";
        public const string SCRIPTRUNNER_VERSION_PROPERTY = "scriptrunner_version";

        private readonly FileInfo mManifestFile;
        protected readonly Dictionary<string, MetadataLoader> mLoaderMap = new Dictionary<string, MetadataLoader>();
        protected readonly List<ManifestEntry> mManifestEntryList = new List<ManifestEntry>();
        protected Dictionary<string, string> mPromotionPropertyMap = null;

        /** A set of all file paths for manifest entries which were processed during a parse and were NOT forced duplicates */
        protected readonly HashSet<string> mFinalProcessedFilePathSet = new HashSet<string>();

        /**
         * Constructs a new ManifestParser which will be used to parse the given file.
         * @param pManifestFile File to be parsed.
         */
        public ManifestParser(FileInfo pManifestFile)
        {
            mManifestFile = pManifestFile;
        }

        /**
         * Parses a manifest file (or manifest override file) and populates this parser's list of ManifestEntries.
         * @throws FileNotFoundException If the manifest file does not exist.
         * @throws IOException If the manifest file cannot be read.
         * @throws ExParser If a line cannot be parsed.
         * @throws ExManifest If a ManifestEntry is invalid in the context of the manifest.
         */
        public void parse()
        {
            StreamReader lReader = null;
            try
            {
                lReader = new StreamReader(mManifestFile.FullName);
                string lLine;
                int lHighestSequencePosition = 0;

                Dictionary<string, int> lFileIndexes = new Dictionary<string, int>();

                while ((lLine = lReader.ReadLine()) != null)
                {

                    //Trim whitespace
                    lLine = lLine.Trim();

                    //Skip empty lines
                    if (string.IsNullOrEmpty(lLine))
                    {
                        continue;
                    }

                    //Skip comment lines
                    if (lLine[0] == '#')
                    {
                        continue;
                    }

                    //Attempt to parse the line as a property line (this will return null if it is not)
                    Dictionary<string, string> lPromotionPropertyMap = parsePromotionPropertiesLine(lLine);
                    if (lPromotionPropertyMap != null)
                    {
                        Logger.logDebug("Parsed promotion property line");
                        if (mPromotionPropertyMap != null)
                        {
                            //Only one definition allowed per manifest
                            throw new ExParser("Duplicate PROMOTION definition line found");
                        }
                        mPromotionPropertyMap = lPromotionPropertyMap;
                        continue;
                    }

                    //Assume any other line is a manifest entry (this will error if it is not)
                    ManifestEntry lManifestEntry = parseManifestEntryLine(lLine, lHighestSequencePosition);

                    lHighestSequencePosition = lManifestEntry.getSequencePosition();
                    mManifestEntryList.Add(lManifestEntry);
                    if (!lManifestEntry.isForcedDuplicate())
                    {
                        mFinalProcessedFilePathSet.Add(lManifestEntry.getFilePath());
                    }

                    //Record the index of the file
                    int lFileIndex;
                    if (!lFileIndexes.TryGetValue(lManifestEntry.getFilePath(), out lFileIndex))
                    {
                        lFileIndex = 1;
                    }
                    else
                    {
                        lFileIndex++;
                    }
                    lManifestEntry.setFileIndex(lFileIndex);
                    lFileIndexes.Add(lManifestEntry.getFilePath(), lFileIndex);

                    //Construct a new loader if necessary
                    createLoaderIfUndefined(lManifestEntry.getLoaderName());

                    Logger.logDebug("Parsed manifest entry for " + lManifestEntry.getFilePath());
                }
            }
            finally
            {
                //Close the reader to release the lock on the file    
                lReader?.Close();
            }
        }

        /**
         * Parses a line of a manifest file into a ManifestEntry.
         * @param pLine Line of file to be parsed.
         * @param pCurrentSequencePosition
         * @return A new ManifestEntry.
         * @throws ExParser If the line cannot be parsed into a ManifestEntry.
         * @throws ExManifest If the ManifestEntry is invalid in the context of this manifest.
         */
        protected ManifestEntry parseManifestEntryLine(string pLine, int pCurrentSequencePosition)
        {
            return ManifestEntry.parseManifestFileLine(pLine, false);
        }

        /**
         * Returns null if not a prop line
         * @param pLine
         * @return
         * @throws ExParser
         */
        protected Dictionary<string, string> parsePromotionPropertiesLine(string pLine)
        {

            Match lMatcher = MANIFEST_LINE_PROMOTION_PROPERTIES_PATTERN.Match(pLine);

            if (lMatcher.Success)
            {
                Dictionary<string, string> lProperties = parsePropertyMapString(lMatcher.Groups[1].Value);
                return lProperties;
            }
            else
            {
                return null;
            }
        }

        /**
         * Creates a new MetadataLoader in this object's loader map, if required.
         * @param pLoaderName Name of loader to create.
         */
        private void createLoaderIfUndefined(string pLoaderName)
        {
            //Check this is not a duplicate definition or a built-in definition
            if (!mLoaderMap.ContainsKey(pLoaderName) && BuiltInLoader.getBuiltInLoaderOrNull(pLoaderName) == null)
            {
                //Construct the loader
                MetadataLoader lLoader = new MetadataLoader(pLoaderName);
                //Register in this parser's map
                mLoaderMap.Add(pLoaderName, lLoader);
            }
        }

        /**
         * Parses a property map string into a Map of name/value pairs. The required format is as follows:<br/><br/>
         * <code>
         * {name="value", name2="value2"}
         * </code><br/><br/>
         * The string may be null - in this case, an empty map is returned.
         * @param pString Property string to be parsed.
         * @return Map of property names to values.
         * @throws ExParser If the string is not a valid property map string.
         */
        public static Dictionary<string, string> parsePropertyMapString(string pString)
        {
            Dictionary<string, string> lResult = new Dictionary<string, string>();

            if (pString == null)
            {
                return null;
            }

            //Validate string is delimited by { and } markers
            pString = pString.Trim();
            if (pString[0] != '{')
            {
                throw new ExParser("Property string must start with '{' character");
            }
            else if (pString[pString.Length - 1] != '}')
            {
                throw new ExParser("Property string must end with '}' character");
            }

            pString = pString.Substring(1, pString.Length - 1);

            StringBuilder lEscapedString = new StringBuilder();
            //Replace quoted commas with replacement tokens
            //TODO what about quotes in values? (i.e. escape character)
            bool lInQuotes = false;
            for (int i = 0; i < pString.Length; i++)
            {
                char lChar = pString[i];
                if (lChar == '"')
                {
                    lInQuotes = !lInQuotes;
                }

                if (lInQuotes && lChar == ',')
                {
                    lEscapedString.Append("##COMMA##");
                }
                else if (lInQuotes && lChar == '=')
                {
                    lEscapedString.Append("##EQUALS##");
                }
                else
                {
                    lEscapedString.Append(lChar);
                }
            }

            if (lInQuotes)
            {
                throw new ExParser("Unterminated quotes in property map string");
            }


            //Split the whole property string into strings of "name=value" name value pairs - use guava so empty results aren't omitted (these are a parser error)
            IEnumerable<string> lNameValuePairs = lEscapedString.ToString().Split(',').Where(x => !string.IsNullOrEmpty(x));

            //Loop each name value pair and add the property names as map keys and values as map values
            foreach (string lNameValuePair in lNameValuePairs)
            {
                string[] lSplit = lNameValuePair.Split(new string[] { "[ \\t]*=[ \\t]*" }, StringSplitOptions.None);

                //There should be exactly 2 elements after splitting on the equals symbol
                if (lSplit.Length != 2)
                {
                    throw new ExParser("Invalid property string: " + lNameValuePair);
                }

                //TODO validate name has no = or , in it
                //Property names are case-insensitive
                string lName = lSplit[0].ToLower();
                //Replace escaped characters back into the value and strip any surrounding quote marks
                string lValue = lSplit[1].Replace("##COMMA##", ",").Replace("##EQUALS##", "=").Replace("\"", "");

                //Check that the name does not alredy exist in the map
                if (lResult.ContainsKey(lName))
                {
                    throw new ExParser("Duplicate property value " + lName);
                }

                //Add property to map      
                lResult.Add(lName, lValue);
            }


            return lResult;
        }

        /**
         * Gets a map of names to Loaders constructed by this parser. Note this map will not contain built-in loaders.
         * @return Loader map.
         */
        public Map<String, MetadataLoader> getLoaderMap()
        {
            return mLoaderMap;
        }

        /**
         * Get the parsed property map for this manifest file. This may be null if the PROMOTION line is not specified.
         * @return Property map.
         */
        public Map<String, String> getPromotionPropertyMap()
        {
            return mPromotionPropertyMap;
        }

        /**
         * Returns a list of all files implicated by this manifest in the order in which they should be promoted.
         * @return Ordered file list.
         */
        public List<ManifestEntry> getManifestEntryList()
        {
            return mManifestEntryList;
        }

    }
}