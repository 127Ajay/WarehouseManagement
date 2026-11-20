using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Wms.ArchitectureTests;

/// <summary>A project and the names of the projects it references.</summary>
public sealed record ProjectInfo(string Name, IReadOnlyList<string> References);

/// <summary>
/// Reference rules from LLD section 1. Checked from .csproj files, so a reference that is
/// declared but not yet used in code is still caught.
/// </summary>
public static partial class ReferenceRules
{
    public const string SharedKernel = "Wms.SharedKernel";
    public const string Abstractions = "Wms.Application.Abstractions";
    public const string InfrastructureCommon = "Wms.Infrastructure.Common";
    public const string WebCommon = "Wms.Web.Common";

    private static readonly Dictionary<string, string[]> BuildingBlockRules = new(StringComparer.Ordinal)
    {
        [SharedKernel] = [],
        [Abstractions] = [SharedKernel],
        [InfrastructureCommon] = [SharedKernel, Abstractions],
        [WebCommon] = [SharedKernel, Abstractions],
    };

    public static IReadOnlyList<ProjectInfo> LoadFrom(string sourceRoot) =>
        [.. Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new ProjectInfo(
                Path.GetFileNameWithoutExtension(path),
                [.. XDocument.Load(path)
                    .Descendants("ProjectReference")
                    .Select(e => Path.GetFileNameWithoutExtension(((string?)e.Attribute("Include") ?? string.Empty).Replace('\\', '/')))
                    .Where(n => n.Length > 0)]))];

    public static IReadOnlyList<string> Check(IEnumerable<ProjectInfo> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        var violations = new List<string>();

        foreach (var project in projects)
        {
            if (project.Name.StartsWith("Wms.Host.", StringComparison.Ordinal))
            {
                continue; // hosts are composition roots and may reference anything
            }

            foreach (var reference in project.References)
            {
                if (!IsAllowed(project.Name, reference))
                {
                    violations.Add($"{project.Name} must not reference {reference}");
                }
            }
        }

        return violations;
    }

    public static bool IsAllowed(string project, string reference)
    {
        if (BuildingBlockRules.TryGetValue(project, out var allowedForBlock))
        {
            return allowedForBlock.Contains(reference, StringComparer.Ordinal);
        }

        if (!TryParseModuleProject(project, out var module, out var layer))
        {
            return false; // unknown project shape: fail loudly so the rules get updated
        }

        var refIsModule = TryParseModuleProject(reference, out var refModule, out var refLayer);
        var sameModule = refIsModule && refModule == module;
        var isAnyContracts = refIsModule && refLayer == "Contracts";

        return layer switch
        {
            "Domain" => reference == SharedKernel,
            "Contracts" => reference == SharedKernel,
            "Application" => reference is SharedKernel or Abstractions
                || (sameModule && refLayer == "Domain")
                || isAnyContracts,
            "Infrastructure" => reference is SharedKernel or Abstractions or InfrastructureCommon
                || (sameModule && refLayer is "Domain" or "Application")
                || isAnyContracts,
            "Endpoints" => reference is SharedKernel or Abstractions or WebCommon
                || (sameModule && refLayer is "Application" or "Contracts" or "Infrastructure"),
            _ => false,
        };
    }

    public static bool TryParseModuleProject(string name, out string module, out string layer)
    {
        var match = ModuleProject().Match(name);
        module = match.Success ? match.Groups["module"].Value : string.Empty;
        layer = match.Success ? match.Groups["layer"].Value : string.Empty;
        return match.Success && !BuildingBlockRules.ContainsKey(name);
    }

    [GeneratedRegex(@"^Wms\.(?<module>[A-Za-z]+)\.(?<layer>Domain|Application|Infrastructure|Contracts|Endpoints)$")]
    private static partial Regex ModuleProject();
}
