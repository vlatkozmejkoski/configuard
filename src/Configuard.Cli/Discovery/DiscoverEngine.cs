using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Configuard.Cli.Validation;

namespace Configuard.Cli.Discovery;

internal static class DiscoverEngine
{
    public static DiscoveryReport Discover(string scanPath)
    {
        var fullScanPath = Path.GetFullPath(scanPath);
        var files = CollectCSharpFiles(fullScanPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var findingsByPath = new Dictionary<string, DiscoveredKeyFinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetRoot();
            foreach (var match in FindMatches(root, file, fullScanPath))
            {
                if (!findingsByPath.TryGetValue(match.Path, out var finding))
                {
                    finding = new DiscoveredKeyFinding
                    {
                        Path = match.Path,
                        Confidence = "high",
                        SuggestedType = "string"
                    };
                    findingsByPath[match.Path] = finding;
                }

                if (!finding.Evidence.Any(e =>
                        string.Equals(e.File, match.Evidence.File, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(e.Symbol, match.Evidence.Symbol, StringComparison.Ordinal) &&
                        string.Equals(e.Pattern, match.Evidence.Pattern, StringComparison.Ordinal)))
                {
                    finding.Evidence.Add(match.Evidence);
                }
            }
        }

        var findings = findingsByPath.Values
            .OrderBy(finding => finding.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var finding in findings)
        {
            finding.Evidence.Sort((left, right) =>
            {
                var fileComparison = string.Compare(left.File, right.File, StringComparison.OrdinalIgnoreCase);
                if (fileComparison != 0)
                {
                    return fileComparison;
                }

                var symbolComparison = string.Compare(left.Symbol, right.Symbol, StringComparison.Ordinal);
                return symbolComparison != 0
                    ? symbolComparison
                    : string.Compare(left.Pattern, right.Pattern, StringComparison.Ordinal);
            });
        }

        return new DiscoveryReport
        {
            Version = "1",
            ScanPath = fullScanPath,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Findings = findings
        };
    }

    private static IEnumerable<string> CollectCSharpFiles(string scanPath)
    {
        if (File.Exists(scanPath))
        {
            return Path.GetExtension(scanPath).Equals(".cs", StringComparison.OrdinalIgnoreCase)
                ? [scanPath]
                : [];
        }

        return Directory
            .EnumerateFiles(scanPath, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<DiscoveryMatch> FindMatches(SyntaxNode root, string filePath, string scanRoot)
    {
        foreach (var elementAccess in root.DescendantNodes().OfType<ElementAccessExpressionSyntax>())
        {
            var keyPath = ReadFirstStringArgument(elementAccess.ArgumentList?.Arguments);
            if (keyPath is null)
            {
                continue;
            }

            yield return BuildMatch(
                keyPath,
                filePath,
                scanRoot,
                symbol: GetSymbol(elementAccess),
                pattern: "indexer");
        }

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName is "GetValue" or "GetSection")
            {
                var keyPath = ReadFirstStringArgument(invocation.ArgumentList.Arguments);
                if (keyPath is null)
                {
                    continue;
                }

                yield return BuildMatch(
                    keyPath,
                    filePath,
                    scanRoot,
                    symbol: GetSymbol(invocation),
                    pattern: methodName);
            }
            else if (methodName == "Configure")
            {
                var sectionPath = TryReadConfigureSectionPath(invocation);
                if (sectionPath is null)
                {
                    continue;
                }

                yield return BuildMatch(
                    sectionPath,
                    filePath,
                    scanRoot,
                    symbol: GetSymbol(invocation),
                    pattern: "Configure(GetSection)");
            }
        }
    }

    private static DiscoveryMatch BuildMatch(
        string keyPath,
        string filePath,
        string scanRoot,
        string symbol,
        string pattern)
    {
        return new DiscoveryMatch(
            Path: RuleEvaluation.NormalizePath(keyPath).Trim(),
            Evidence: new DiscoveryEvidence
            {
                File = Path.GetRelativePath(scanRoot, filePath),
                Symbol = symbol,
                Pattern = pattern
            });
    }

    private static string? ReadFirstStringArgument(SeparatedSyntaxList<ArgumentSyntax>? arguments)
    {
        if (arguments is null || arguments.Value.Count == 0)
        {
            return null;
        }

        return arguments.Value[0].Expression is LiteralExpressionSyntax literal &&
               literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;
    }

    private static string? TryReadConfigureSectionPath(InvocationExpressionSyntax configureInvocation)
    {
        if (configureInvocation.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        var firstArgument = configureInvocation.ArgumentList.Arguments[0].Expression;
        if (firstArgument is not InvocationExpressionSyntax nestedInvocation ||
            nestedInvocation.Expression is not MemberAccessExpressionSyntax nestedMember ||
            !string.Equals(nestedMember.Name.Identifier.Text, "GetSection", StringComparison.Ordinal))
        {
            return null;
        }

        return ReadFirstStringArgument(nestedInvocation.ArgumentList.Arguments);
    }

    private static string GetSymbol(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        if (method is not null)
        {
            return method switch
            {
                MethodDeclarationSyntax methodDeclaration => methodDeclaration.Identifier.ValueText,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
                _ => method.Kind().ToString()
            };
        }

        var type = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        return type?.Identifier.ValueText ?? "(unknown)";
    }

    private sealed record DiscoveryMatch(string Path, DiscoveryEvidence Evidence);
}
