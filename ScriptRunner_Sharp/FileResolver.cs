namespace ScriptRunner_Sharp
{
    using System.IO;

    /**
     * Interface for encapsulation the ScriptRunner and ManifestBuilder classes, both of which are required to resolve a file from
     * a relative path.
     */
    public interface FileResolver {

        /**
         * Resolves a relative file path to an actual file.
         * @param pRelativePath A path to the desired file, relative to the base directory.
         * @return The requested file.
         * @throws FileNotFoundException If the file does not exist.
         */
        FileInfo resolveFile(string pRelativePath);

        /**
         * Gets the base directory which relative paths will be evaluated from.
         * @return Base directory.
         */
        FileInfo getBaseDirectory();

        /**
         * Relativises the path of the given file using a base directory and returns the path string.
         * @param pFile File to get relativised path for.
         * @return Relativised path.
         */
        string relativeFilePath(FileInfo pFile);
    }
}