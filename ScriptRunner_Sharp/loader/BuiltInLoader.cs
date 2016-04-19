
using System.Collections.Generic;
/**
* Marker class for all built-in loaders in ScriptRunner. This provides static methods for retriving the singleton
* built-in loaders.
*/
public abstract class BuiltInLoader : Loader
{

    public const string LOADER_NAME_DB_SOURCE = "DatabaseSource";
    public const string LOADER_NAME_SCRIPTRUNNER_UTIL = "ScriptRunnerUtil";
    public const string LOADER_NAME_PATCH = "Patch";
    /** Special loader name for the builder only - files associated with this loader are ignored when constructing the manifest. */
    public const string LOADER_NAME_IGNORE = "Ignore";

    private static readonly Dictionary<string, Loader> gBuiltInLoaderMap;
    static BuiltInLoader()
    {
        gBuiltInLoaderMap = new Dictionary<string, Loader>();
        gBuiltInLoaderMap.Add(LOADER_NAME_DB_SOURCE, new DatabaseSourceLoader());
        gBuiltInLoaderMap.Add(LOADER_NAME_SCRIPTRUNNER_UTIL, UtilLoader.getInstance());
        gBuiltInLoaderMap.Add(LOADER_NAME_PATCH, new PatchScriptLoader());
    }

    /**
     * Get the built in loader corresponding to the given name, or null if it does not exist.
     * @param pLoaderName Name of required loader.
     * @return Built-in loader.
     */
    public static Loader getBuiltInLoaderOrNull(string pLoaderName)
    {
        Loader loader;
        gBuiltInLoaderMap.TryGetValue(pLoaderName, out loader);
        return loader;
    }

    /**
     * Gets the map of built-in loaders. The key of the map is the loader name.
     * @return Map of names to built-in loaders.
     */
    public static Dictionary<string, Loader> getBuiltInLoaderMap()
    {
        return gBuiltInLoaderMap;
    }

}
