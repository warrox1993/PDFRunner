using System;
using System.IO;

namespace ConvertPDF.Services
{
    /// <summary>
    /// Handles file naming conflicts during conversion.
    /// Supports: Skip, Overwrite, AutoRename modes.
    /// </summary>
    public enum ConflictResolution
    {
        Skip,
        Overwrite,
        AutoRename
    }

    public class ConflictResolver
    {
        public ConflictResolution DefaultMode { get; set; } = ConflictResolution.AutoRename;

        /// <summary>
        /// Resolves a file path conflict based on the current mode.
        /// Returns null if the file should be skipped.
        /// </summary>
        public string ResolveConflict(string proposedPath)
        {
            if (!File.Exists(proposedPath))
                return proposedPath; // No conflict

            switch (DefaultMode)
            {
                case ConflictResolution.Skip:
                    return null; // Signal to skip this file

                case ConflictResolution.Overwrite:
                    return proposedPath; // Use same path, will overwrite

                case ConflictResolution.AutoRename:
                default:
                    return GenerateUniquePath(proposedPath);
            }
        }

        private string GenerateUniquePath(string path)
        {
            string directory = Path.GetDirectoryName(path) ?? "";
            string nameWithoutExt = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);

            int counter = 1;
            string newPath;

            do
            {
                newPath = Path.Combine(directory, $"{nameWithoutExt} ({counter}){extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }
    }
}
