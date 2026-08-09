using System.Text.RegularExpressions;

namespace SmartCopy.Core;

public static class GitTagParser
{
    public static IEnumerable<Version> ParseVersions(string lsRemoteOutput)
        => Regex.Matches(lsRemoteOutput, @"refs/tags/(?:v)?(\d+\.\d+\.\d+)(?:\^\{\})?(?=\s|$)")
            .Select(m => TryParse(m.Groups[1].Value))
            .Where(v => v is not null)
            .Cast<Version>();

    private static Version? TryParse(string text)
        => Version.TryParse(text, out var v) ? v : null;
}
