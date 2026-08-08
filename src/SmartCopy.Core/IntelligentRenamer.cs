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
        => sourcePaths.Select(p => new TransferItem(p, GenerateSmartFilePath(p, destinationFolder))).ToArray();

    public string GenerateSmartFilePath(string sourceFilePath, string destinationFolderPath)
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
        while (File.Exists(finalPath));

        return finalPath;
    }
}
