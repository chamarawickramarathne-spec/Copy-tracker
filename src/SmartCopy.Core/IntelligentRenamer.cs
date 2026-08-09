namespace SmartCopy.Core;

public enum RenameScheme
{
    FolderBased = 0,
    Sequential = 1,
}

public sealed class IntelligentRenamer
{
    private readonly RenameScheme _scheme;

    public IntelligentRenamer(RenameScheme scheme = RenameScheme.FolderBased) => _scheme = scheme;

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

        int count = 0;
        try
        {
            foreach (var _ in Directory.EnumerateFiles(destinationFolderPath, $"*{extension}")) count++;
        }
        catch
        {
            // folder may be temporarily inaccessible; collision check below still protects us
        }

        string newFileName;
        string finalPath;
        int attempt = count + 1;
        do
        {
            string baseName = _scheme == RenameScheme.Sequential ? stem : folderName;
            newFileName = $"{baseName}_{attempt}{extension}";
            finalPath = Path.Combine(destinationFolderPath, newFileName);
            attempt++;
        }
        while (File.Exists(finalPath) || (reserved is not null && reserved.Contains(finalPath)));

        return finalPath;
    }
}
