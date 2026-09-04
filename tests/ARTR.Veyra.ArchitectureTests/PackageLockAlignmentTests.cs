using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace ARTR.Veyra.ArchitectureTests;

public sealed class PackageLockAlignmentTests
{
    [Fact]
    public void MicrosoftOpenApi_RemainsPinnedAt_2_11_0()
    {
        var cpm = LoadCpmVersions();
        Assert.True(cpm.TryGetValue("Microsoft.OpenApi", out var version), "Microsoft.OpenApi missing from Directory.Packages.props.");
        Assert.Equal("2.11.0", version);

        foreach (var lockPath in EnumerateLockFiles())
        {
            foreach (var entry in EnumerateLockEntries(lockPath))
            {
                if (!entry.Id.Equals("Microsoft.OpenApi", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.Type is not ("Direct" or "CentralTransitive"))
                {
                    continue;
                }

                Assert.Equal("2.11.0", entry.Resolved);
                Assert.Equal("2.11.0", ParseRequestedLowerBound(entry.Requested));
            }
        }
    }

    [Fact]
    public void RestoreLockedMode_IsEnabledWhenCiIsTrue_AndNotDisabled()
    {
        var props = File.ReadAllText(Path.Combine(FindRepoRoot(), "Directory.Build.props"));
        Assert.Contains("<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>", props, StringComparison.Ordinal);
        Assert.Contains("""<RestoreLockedMode Condition="'$(CI)' == 'true'">true</RestoreLockedMode>""", props, StringComparison.Ordinal);
        Assert.DoesNotContain("<RestoreLockedMode>false</RestoreLockedMode>", props, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RestoreLockedMode>false", props, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CiWorkflow_RestoreStep_StillUsesLockedMode()
    {
        var ci = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github", "workflows", "ci.yml"));
        Assert.Contains("dotnet restore ARTR.Veyra.sln --locked-mode", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("--force-evaluate", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreLockedMode=false", ci, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RestoreLockedMode: false", ci, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowDotnetRestoreSln_AlwaysPassesLockedMode()
    {
        var workflowsDir = Path.Combine(FindRepoRoot(), ".github", "workflows");
        var mismatches = new List<string>();
        foreach (var yml in Directory.EnumerateFiles(workflowsDir, "*.yml"))
        {
            var text = File.ReadAllText(yml);
            foreach (Match match in Regex.Matches(text, @"dotnet\s+restore\s+ARTR\.Veyra\.sln(?<flags>[^\r\n]*)"))
            {
                var flags = match.Groups["flags"].Value;
                if (!flags.Contains("--locked-mode", StringComparison.Ordinal))
                {
                    mismatches.Add($"{Path.GetFileName(yml)}: {match.Value.Trim()}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, "Workflow restore without --locked-mode: " + string.Join("; ", mismatches));
    }

    [Fact]
    public void EverySolutionProject_HasPackagesLockFile()
    {
        var root = FindRepoRoot();
        var missing = new List<string>();
        foreach (var csproj in EnumerateSolutionProjects(root))
        {
            var lockPath = Path.Combine(Path.GetDirectoryName(csproj)!, "packages.lock.json");
            if (!File.Exists(lockPath))
            {
                missing.Add(Path.GetRelativePath(root, csproj));
            }
        }

        Assert.True(missing.Count == 0, "Projects missing packages.lock.json: " + string.Join(", ", missing));
    }

    [Fact]
    public void DirectAndCentralTransitive_RequestedVersions_MatchCpm()
    {
        var cpm = LoadCpmVersions();
        var mismatches = new List<string>();

        foreach (var lockPath in EnumerateLockFiles())
        {
            var relative = Path.GetRelativePath(FindRepoRoot(), lockPath);
            foreach (var entry in EnumerateLockEntries(lockPath))
            {
                if (entry.Type is not ("Direct" or "CentralTransitive"))
                {
                    continue;
                }

                if (!cpm.TryGetValue(entry.Id, out var cpmVersion))
                {
                    continue;
                }

                var requested = ParseRequestedLowerBound(entry.Requested);
                if (!string.Equals(requested, cpmVersion, StringComparison.Ordinal)
                    || !string.Equals(entry.Resolved, cpmVersion, StringComparison.Ordinal))
                {
                    mismatches.Add(
                        $"{relative} {entry.Type} {entry.Id}: requested={entry.Requested} resolved={entry.Resolved} cpm={cpmVersion}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    [Fact]
    public void LockFiles_HaveNoStaleMicrosoftExtensions_10_0_10()
    {
        var leftovers = new List<string>();
        foreach (var lockPath in EnumerateLockFiles())
        {
            var relative = Path.GetRelativePath(FindRepoRoot(), lockPath);
            foreach (var entry in EnumerateLockEntries(lockPath))
            {
                if (entry.Type is not ("Direct" or "CentralTransitive"))
                {
                    continue;
                }

                var isMicrosoftPin = entry.Id.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase)
                    || entry.Id.StartsWith("Microsoft.AspNetCore.", StringComparison.OrdinalIgnoreCase);
                if (!isMicrosoftPin)
                {
                    continue;
                }

                var requested = ParseRequestedLowerBound(entry.Requested);
                if (requested == "10.0.10" || entry.Resolved == "10.0.10")
                {
                    leftovers.Add($"{relative} {entry.Type} {entry.Id} requested={entry.Requested} resolved={entry.Resolved}");
                }
            }
        }

        Assert.True(leftovers.Count == 0, string.Join(Environment.NewLine, leftovers));
    }

    private static Dictionary<string, string> LoadCpmVersions()
    {
        var path = Path.Combine(FindRepoRoot(), "Directory.Packages.props");
        var doc = XDocument.Load(path);
        return doc.Descendants("PackageVersion")
            .Select(el => (
                Id: (string?)el.Attribute("Include"),
                Version: (string?)el.Attribute("Version")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Id) && !string.IsNullOrWhiteSpace(pair.Version))
            .ToDictionary(pair => pair.Id!, pair => pair.Version!, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateLockFiles()
    {
        var root = FindRepoRoot();
        return Directory.EnumerateFiles(root, "packages.lock.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateSolutionProjects(string root)
    {
        var sln = File.ReadAllLines(Path.Combine(root, "ARTR.Veyra.sln"));
        foreach (var line in sln)
        {
            var match = Regex.Match(line, @"Project\(""[^""]+""\)\s*=\s*""[^""]+"",\s*""([^""]+\.csproj)""");
            if (!match.Success)
            {
                continue;
            }

            yield return Path.GetFullPath(Path.Combine(root, match.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar)));
        }
    }

    private static IEnumerable<LockEntry> EnumerateLockEntries(string lockPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(lockPath));
        if (!doc.RootElement.TryGetProperty("dependencies", out var dependencies))
        {
            yield break;
        }

        foreach (var tfm in dependencies.EnumerateObject())
        {
            foreach (var package in tfm.Value.EnumerateObject())
            {
                var type = package.Value.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "" : "";
                var requested = package.Value.TryGetProperty("requested", out var requestedEl) ? requestedEl.GetString() ?? "" : "";
                var resolved = package.Value.TryGetProperty("resolved", out var resolvedEl) ? resolvedEl.GetString() ?? "" : "";
                yield return new LockEntry(package.Name, type, requested, resolved);
            }
        }
    }

    private static string ParseRequestedLowerBound(string requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return "";
        }

        var text = requested.Trim();
        if (text.StartsWith('[') || text.StartsWith('('))
        {
            text = text[1..];
        }

        var comma = text.IndexOf(',');
        if (comma < 0)
        {
            return text.TrimEnd(']', ')');
        }

        return text[..comma].Trim();
    }

    private static string FindRepoRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ARTR.Veyra.sln"))
                    && File.Exists(Path.Combine(dir.FullName, "Directory.Packages.props")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repository root from test context.");
    }

    private sealed record LockEntry(string Id, string Type, string Requested, string Resolved);
}
