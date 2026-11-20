using System.Text.RegularExpressions;

namespace Wms.ArchitectureTests;

/// <summary>APIs that must not appear in production code, with the approved replacement.</summary>
public static partial class BannedApis
{
    private static readonly (Regex Pattern, string Reason)[] Rules =
    [
        (ClockPattern(), "read time through IClock"),
        (NewGuidPattern(), "use Ids.New() (UUID v7)"),
    ];

    // The one file allowed to read the system clock.
    private static readonly string[] Allowed = ["SystemClock.cs"];

    public static IReadOnlyList<string> Scan(string sourceRoot) =>
        [.. Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal))
            .Where(path => !Allowed.Contains(Path.GetFileName(path), StringComparer.Ordinal))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, number: index + 1)))
            .Where(x => !x.line.TrimStart().StartsWith("//", StringComparison.Ordinal)) // comments may name banned APIs
            .SelectMany(x => Rules
                .Where(r => r.Pattern.IsMatch(x.line))
                .Select(r => $"{Path.GetRelativePath(sourceRoot, x.path)}:{x.number}: {r.Reason}"))];

    [GeneratedRegex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b")]
    private static partial Regex ClockPattern();

    [GeneratedRegex(@"\bGuid\.NewGuid\(\)")]
    private static partial Regex NewGuidPattern();
}
