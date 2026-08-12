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
        var items = new List<TransferItem>();
        foreach (string source in sourcePaths)
        {
            string destination = GenerateSmartFilePath(source, destinationFolder, reserved);
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
        string extension = Path.GetExtension(sourceFilePath);
        string stem = Path.GetFileNameWithoutExtension(sourceFilePath);
        string folderName = new DirectoryInfo(destinationFolderPath).Name;
        if (string.IsNullOrWhiteSpace(folderName)) folderName = stem;

        var usedNumbers = new HashSet<int>();
        int count = 0;
        try
        {
            foreach (string file in Directory.EnumerateFiles(destinationFolderPath, $"*{extension}"))
            {
                count++;
                if (TryGetTrailingNumber(Path.GetFileNameWithoutExtension(file), out int number))
                    usedNumbers.Add(number);
            }
        }
        catch
        {
            // folder may be temporarily inaccessible; collision check below still protects us
        }

        if (reserved is not null)
        {
            foreach (string path in reserved)
            {
                if (!Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase)) continue;
                if (TryGetTrailingNumber(Path.GetFileNameWithoutExtension(path), out int number))
                    usedNumbers.Add(number);
            }
        }

        int nextNumber = Math.Max(count, 1);
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
}
