namespace SmartCopy.Core;

public enum RenameFormat
{
    UnderscoreWithName = 0,
    SpaceWithName = 1,
    SpaceFolderNumber = 2,
}

public sealed class IntelligentRenamer
{
    private readonly RenameFormat _format;

    public IntelligentRenamer(RenameFormat format = RenameFormat.UnderscoreWithName)
    {
        _format = format;
    }

    public IReadOnlyList<TransferItem> BuildItems(IEnumerable<string> sourcePaths, string destinationFolder)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var snapshot = DestinationSnapshot.Scan(destinationFolder);
        var items = new List<TransferItem>();
        foreach (string source in sourcePaths)
        {
            string destination = GenerateSmartFilePath(source, destinationFolder, snapshot, reserved);
            reserved.Add(destination);
            items.Add(new TransferItem(source, destination));
        }
        return items;
    }

    public string GenerateSmartFilePath(
        string sourceFilePath,
        string destinationFolderPath,
        ISet<string>? reserved = null)
    {
        var snapshot = DestinationSnapshot.Scan(destinationFolderPath);
        return GenerateSmartFilePath(sourceFilePath, destinationFolderPath, snapshot, reserved);
    }

    private string GenerateSmartFilePath(
        string sourceFilePath,
        string destinationFolderPath,
        DestinationSnapshot snapshot,
        ISet<string>? reserved)
    {
        string extension = Path.GetExtension(sourceFilePath);
        string stem = Path.GetFileNameWithoutExtension(sourceFilePath);
        string folderName = new DirectoryInfo(destinationFolderPath).Name;
        if (string.IsNullOrWhiteSpace(folderName)) folderName = stem;

        ExtensionStats stats = snapshot.StatsFor(extension);
        var usedNumbers = new HashSet<int>(stats.Numbers);
        if (reserved is not null)
        {
            foreach (string path in reserved)
            {
                if (!Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase)) continue;
                if (TryGetTrailingNumber(Path.GetFileNameWithoutExtension(path), out int number))
                    usedNumbers.Add(number);
            }
        }

        int nextNumber = Math.Max(stats.Count, 1);
        string finalPath;
        do
        {
            string newFileName = BuildFileName(stem, folderName, nextNumber, extension);
            finalPath = Path.Combine(destinationFolderPath, newFileName);
            nextNumber++;
        }
        while (usedNumbers.Contains(nextNumber - 1) || File.Exists(finalPath) || (reserved is not null && reserved.Contains(finalPath)));

        return finalPath;
    }

    private string BuildFileName(string stem, string folderName, int number, string extension) => _format switch
    {
        RenameFormat.SpaceWithName => $"{stem} {folderName} {number}{extension}",
        RenameFormat.SpaceFolderNumber => $"{folderName} {number}{extension}",
        _ => $"{stem}_{folderName}_{number}{extension}",
    };

    private static bool TryGetTrailingNumber(string fileNameWithoutExtension, out int number)
    {
        int separator = Math.Max(fileNameWithoutExtension.LastIndexOf('_'), fileNameWithoutExtension.LastIndexOf(' '));
        if (separator < 0 || separator == fileNameWithoutExtension.Length - 1)
        {
            number = 0;
            return false;
        }
        return int.TryParse(fileNameWithoutExtension[(separator + 1)..], out number);
    }

    internal sealed class ExtensionStats
    {
        public int Count { get; internal set; }
        public HashSet<int> Numbers { get; } = new();
    }

    /// <summary>
    /// One-shot scan of a destination folder so a whole paste batch needs a single
    /// directory enumeration instead of one per source file.
    /// </summary>
    internal sealed class DestinationSnapshot
    {
        private readonly Dictionary<string, ExtensionStats> _byExtension = new(StringComparer.OrdinalIgnoreCase);
        private readonly ExtensionStats _allFiles = new();

        private DestinationSnapshot() { }

        public static DestinationSnapshot Scan(string destinationFolderPath)
        {
            var snapshot = new DestinationSnapshot();
            try
            {
                foreach (string file in Directory.EnumerateFiles(destinationFolderPath))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    Record(snapshot._allFiles, name);

                    ExtensionStats stats = snapshot.StatsFor(Path.GetExtension(file));
                    Record(stats, name);
                }
            }
            catch
            {
                // folder may be temporarily inaccessible; collision check below still protects us
            }
            return snapshot;
        }

        public ExtensionStats StatsFor(string extension)
        {
            if (extension.Length == 0) return _allFiles;
            if (!_byExtension.TryGetValue(extension, out var stats))
            {
                stats = new ExtensionStats();
                _byExtension.Add(extension, stats);
            }
            return stats;
        }

        private static void Record(ExtensionStats stats, string fileNameWithoutExtension)
        {
            stats.Count++;
            if (TryGetTrailingNumber(fileNameWithoutExtension, out int number))
                stats.Numbers.Add(number);
        }
    }
}
