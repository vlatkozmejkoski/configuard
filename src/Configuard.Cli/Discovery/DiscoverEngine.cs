using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Configuard.Cli.Validation;

namespace Configuard.Cli.Discovery;

internal static class DiscoverEngine
{
    private const string DotnetSolutionPreset = "dotnet-solution";
    private const string HighConfidence = "high";
    private const string MediumConfidence = "medium";
    private const string LowConfidence = "low";
    private const string UnresolvedSegmentNote = "Contains unresolved dynamic segment(s).";
    private const string UnresolvedPathNote = "Path is unresolved due to runtime indirection.";

    public static Func<DateTimeOffset> UtcNowProvider { get; set; } = () => DateTimeOffset.UtcNow;

    public static DiscoveryReport Discover(
        string scanPath,
        string? scopePreset = null,
        IReadOnlyList<string>? includePatterns = null,
        IReadOnlyList<string>? excludePatterns = null)
    {
        var fullScanPath = Path.GetFullPath(scanPath);
        var files = ApplyFileFilters(
                CollectCSharpFiles(fullScanPath),
                fullScanPath,
                scopePreset,
                includePatterns,
                excludePatterns)
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
                        Confidence = match.Confidence,
                        SuggestedType = match.SuggestedType
                    };
                    finding.Notes.AddRange(match.Notes);
                    findingsByPath[match.Path] = finding;
                }
                else if (GetConfidenceRank(match.Confidence) > GetConfidenceRank(finding.Confidence))
                {
                    finding.Confidence = match.Confidence;
                }

                if (string.Equals(finding.SuggestedType, "string", StringComparison.Ordinal) &&
                    !string.Equals(match.SuggestedType, "string", StringComparison.Ordinal))
                {
                    finding.SuggestedType = match.SuggestedType;
                }

                foreach (var note in match.Notes)
                {
                    if (!finding.Notes.Contains(note, StringComparer.Ordinal))
                    {
                        finding.Notes.Add(note);
                    }
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
            GeneratedAtUtc = UtcNowProvider(),
            Findings = findings
        };
    }

    private static IEnumerable<string> CollectCSharpFiles(string scanPath)
    {
        if (File.Exists(scanPath))
        {
            var extension = Path.GetExtension(scanPath);
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return [scanPath];
            }

            if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(scanPath);
                return string.IsNullOrWhiteSpace(parent)
                    ? []
                    : EnumerateCSharpFilesUnder(parent);
            }

            return [];
        }

        return EnumerateCSharpFilesUnder(scanPath);
    }

    private static IEnumerable<string> EnumerateCSharpFilesUnder(string rootDirectory) =>
        Directory
            .EnumerateFiles(rootDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> ApplyFileFilters(
        IEnumerable<string> files,
        string scanPath,
        string? scopePreset,
        IReadOnlyList<string>? includePatterns,
        IReadOnlyList<string>? excludePatterns)
    {
        TryNormalizeScopePreset(scopePreset, out var normalizedScopePreset, out _);
        var preset = ResolveScopePreset(normalizedScopePreset);
        var includes = NormalizePatterns((includePatterns ?? []).Concat(preset.IncludePatterns).ToList());
        var excludes = NormalizePatterns((excludePatterns ?? []).Concat(preset.ExcludePatterns).ToList());

        foreach (var file in files)
        {
            var relativePath = NormalizePath(Path.GetRelativePath(scanPath, file));

            if (includes.Count > 0 && !includes.Any(pattern => GlobMatches(pattern, relativePath)))
            {
                continue;
            }

            if (excludes.Count > 0 && excludes.Any(pattern => GlobMatches(pattern, relativePath)))
            {
                continue;
            }

            yield return file;
        }
    }

    public static bool TryNormalizeScopePreset(string? rawPreset, out string? normalizedPreset, out string? error)
    {
        normalizedPreset = null;
        error = null;
        if (string.IsNullOrWhiteSpace(rawPreset))
        {
            return true;
        }

        normalizedPreset = rawPreset.Trim().ToLowerInvariant();
        if (normalizedPreset == DotnetSolutionPreset)
        {
            return true;
        }

        error = $"Unsupported discover preset '{rawPreset}'. Supported: {DotnetSolutionPreset}.";
        return false;
    }

    private static ScopePreset ResolveScopePreset(string? normalizedScopePreset)
    {
        if (string.Equals(normalizedScopePreset, DotnetSolutionPreset, StringComparison.Ordinal))
        {
            return new ScopePreset(
                IncludePatterns: [],
                ExcludePatterns:
                [
                    "**/bin/**",
                    "**/obj/**",
                    "**/TestResults/**",
                    "**/.git/**",
                    "**/.vs/**"
                ]);
        }

        return new ScopePreset([], []);
    }

    private static List<string> NormalizePatterns(IReadOnlyList<string>? patterns)
    {
        if (patterns is null || patterns.Count == 0)
        {
            return [];
        }

        return [.. patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static bool GlobMatches(string pattern, string relativePath)
    {
        var regexPattern = "^" +
                           Regex.Escape(pattern)
                               .Replace(@"\*\*", ".*", StringComparison.Ordinal)
                               .Replace(@"\*", @"[^/]*", StringComparison.Ordinal)
                               .Replace(@"\?", @"[^/]", StringComparison.Ordinal) +
                           "$";

        return Regex.IsMatch(relativePath, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');

    private static IEnumerable<DiscoveryMatch> FindMatches(SyntaxNode root, string filePath, string scanRoot)
    {
        foreach (var elementAccess in root.DescendantNodes().OfType<ElementAccessExpressionSyntax>())
        {
            var keyPath = TryResolveFirstPathArgument(elementAccess.ArgumentList?.Arguments);
            if (keyPath is null)
            {
                continue;
            }

            if (string.Equals(keyPath.Confidence, LowConfidence, StringComparison.Ordinal) &&
                !IsConfigurationLikeExpression(elementAccess.Expression))
            {
                continue;
            }

            yield return BuildMatch(
                keyPath,
                filePath,
                scanRoot,
                symbol: GetSymbol(elementAccess),
                pattern: "indexer",
                suggestedType: "string");
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
                var keyPath = TryResolveFirstPathArgument(invocation.ArgumentList.Arguments);
                if (keyPath is null)
                {
                    continue;
                }

                yield return BuildMatch(
                    keyPath,
                    filePath,
                    scanRoot,
                    symbol: GetSymbol(invocation),
                    pattern: methodName,
                    suggestedType: GetSuggestedTypeForInvocation(methodName, memberAccess.Name));
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
                    pattern: "Configure(GetSection)",
                    suggestedType: "object");
            }
            else if (methodName == "Bind")
            {
                var bindPath = TryReadBindPath(invocation);
                if (bindPath is null)
                {
                    continue;
                }

                var pattern = bindPath.Source switch
                {
                    BindPathSource.GetSection => "Bind(GetSection)",
                    BindPathSource.Literal => "Bind(literal)",
                    _ => "Bind"
                };

                yield return BuildMatch(
                    bindPath.PathResolution,
                    filePath,
                    scanRoot,
                    symbol: GetSymbol(invocation),
                    pattern: pattern,
                    suggestedType: bindPath.Source == BindPathSource.GetSection ? "object" : "string");
            }
            else if (methodName == "BindConfiguration")
            {
                var keyPath = TryResolveFirstPathArgument(invocation.ArgumentList.Arguments);
                if (keyPath is null)
                {
                    continue;
                }

                yield return BuildMatch(
                    keyPath,
                    filePath,
                    scanRoot,
                    symbol: GetSymbol(invocation),
                    pattern: "BindConfiguration",
                    suggestedType: "object");
            }
        }
    }

    private static DiscoveryMatch BuildMatch(
        PathResolution pathResolution,
        string filePath,
        string scanRoot,
        string symbol,
        string pattern,
        string suggestedType)
    {
        return new DiscoveryMatch(
            Path: RuleEvaluation.NormalizePath(pathResolution.Path).Trim(),
            Confidence: pathResolution.Confidence,
            SuggestedType: suggestedType,
            Notes: pathResolution.Notes,
            Evidence: new DiscoveryEvidence
            {
                File = Path.GetRelativePath(scanRoot, filePath),
                Symbol = symbol,
                Pattern = pattern
            });
    }

    private static PathResolution? TryResolveFirstPathArgument(SeparatedSyntaxList<ArgumentSyntax>? arguments)
    {
        if (arguments is null || arguments.Value.Count == 0)
        {
            return null;
        }

        return TryResolvePath(arguments.Value[0].Expression);
    }

    private static PathResolution? TryReadConfigureSectionPath(InvocationExpressionSyntax configureInvocation)
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

        return TryResolveFirstPathArgument(nestedInvocation.ArgumentList.Arguments);
    }

    private static BindPath? TryReadBindPath(InvocationExpressionSyntax bindInvocation)
    {
        if (bindInvocation.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        var firstArgument = bindInvocation.ArgumentList.Arguments[0].Expression;
        if (firstArgument is InvocationExpressionSyntax nestedInvocation &&
            nestedInvocation.Expression is MemberAccessExpressionSyntax nestedMember &&
            string.Equals(nestedMember.Name.Identifier.Text, "GetSection", StringComparison.Ordinal))
        {
            var path = TryResolveFirstPathArgument(nestedInvocation.ArgumentList.Arguments);
            return path is null
                ? null
                : new BindPath(path, BindPathSource.GetSection);
        }

        var directPath = TryResolvePath(firstArgument);
        if (directPath is not null)
        {
            return new BindPath(directPath, BindPathSource.Literal);
        }

        return null;
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

    private static string GetSuggestedTypeForInvocation(string methodName, SimpleNameSyntax memberName)
    {
        if (string.Equals(methodName, "GetSection", StringComparison.Ordinal))
        {
            return "object";
        }

        if (!string.Equals(methodName, "GetValue", StringComparison.Ordinal))
        {
            return "string";
        }

        if (memberName is not GenericNameSyntax genericName || genericName.TypeArgumentList.Arguments.Count == 0)
        {
            return "string";
        }

        return MapClrTypeToContractType(genericName.TypeArgumentList.Arguments[0]);
    }

    private static string MapClrTypeToContractType(TypeSyntax typeSyntax)
    {
        if (typeSyntax is NullableTypeSyntax nullableType)
        {
            return MapClrTypeToContractType(nullableType.ElementType);
        }

        if (typeSyntax is ArrayTypeSyntax)
        {
            return "array";
        }

        var typeName = typeSyntax.ToString().Trim();
        var normalized = typeName switch
        {
            "string" => "string",
            "bool" => "bool",
            "int" or "long" or "short" or "byte" or "sbyte" or "uint" or "ulong" or "ushort" => "int",
            "double" or "float" or "decimal" => "number",
            _ => null
        };

        if (normalized is not null)
        {
            return normalized;
        }

        if (typeSyntax is GenericNameSyntax generic)
        {
            var genericTypeName = generic.Identifier.ValueText;
            if (genericTypeName is "IEnumerable" or "ICollection" or "IList" or "IReadOnlyList" or "List")
            {
                return "array";
            }
        }

        return "object";
    }

    private static bool IsConfigurationLikeExpression(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier =>
                identifier.Identifier.ValueText.Contains("config", StringComparison.OrdinalIgnoreCase),
            MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText.Contains("config", StringComparison.OrdinalIgnoreCase) ||
                IsConfigurationLikeExpression(memberAccess.Expression),
            InvocationExpressionSyntax invocation => IsConfigurationLikeInvocation(invocation),
            ParenthesizedExpressionSyntax parenthesized => IsConfigurationLikeExpression(parenthesized.Expression),
            ElementAccessExpressionSyntax nestedAccess => IsConfigurationLikeExpression(nestedAccess.Expression),
            _ => false
        };
    }

    private static bool IsConfigurationLikeInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (methodName is "GetSection" or "GetRequiredSection")
        {
            return true;
        }

        return IsConfigurationLikeExpression(memberAccess.Expression);
    }

    private static PathResolution? TryResolvePath(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                new PathResolution(literal.Token.ValueText, HighConfidence, []),
            ParenthesizedExpressionSyntax parenthesized => TryResolvePath(parenthesized.Expression),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) =>
                TryResolveBinaryPath(binary),
            InterpolatedStringExpressionSyntax interpolated =>
                TryResolveInterpolatedPath(interpolated),
            _ => CreateLowConfidencePath()
        };
    }

    private static PathResolution? TryResolveBinaryPath(BinaryExpressionSyntax binary)
    {
        var left = TryResolvePath(binary.Left);
        var right = TryResolvePath(binary.Right);
        if (left is null && right is null)
        {
            return null;
        }

        var notes = new List<string>();
        if (left is not null)
        {
            notes.AddRange(left.Notes);
        }

        if (right is not null)
        {
            notes.AddRange(right.Notes);
        }

        var hasUnresolvedSegment =
            left is null ||
            right is null ||
            string.Equals(left?.Confidence, LowConfidence, StringComparison.Ordinal) ||
            string.Equals(right?.Confidence, LowConfidence, StringComparison.Ordinal);
        if (hasUnresolvedSegment && !notes.Contains(UnresolvedSegmentNote, StringComparer.Ordinal))
        {
            notes.Add(UnresolvedSegmentNote);
        }

        var path = $"{left?.Path ?? "{expr}"}{right?.Path ?? "{expr}"}";
        var hasLiteralContext = !string.Equals(path, "{expr}{expr}", StringComparison.Ordinal);
        var confidence = hasUnresolvedSegment
            ? (hasLiteralContext ? MediumConfidence : LowConfidence)
            : (string.Equals(left?.Confidence, MediumConfidence, StringComparison.Ordinal) ||
               string.Equals(right?.Confidence, MediumConfidence, StringComparison.Ordinal)
                ? MediumConfidence
                : HighConfidence);

        return new PathResolution(path, confidence, notes);
    }

    private static PathResolution? TryResolveInterpolatedPath(InterpolatedStringExpressionSyntax interpolated)
    {
        var parts = new List<string>();
        var notes = new List<string>();
        var hasUnresolvedSegment = false;

        foreach (var content in interpolated.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
            {
                parts.Add(text.TextToken.ValueText);
                continue;
            }

            if (content is not InterpolationSyntax interpolation)
            {
                continue;
            }

            var interpolationPath = TryResolvePath(interpolation.Expression);
            if (interpolationPath is null)
            {
                parts.Add("{expr}");
                hasUnresolvedSegment = true;
                continue;
            }

            parts.Add(interpolationPath.Path);
            notes.AddRange(interpolationPath.Notes);
            if (string.Equals(interpolationPath.Confidence, MediumConfidence, StringComparison.Ordinal) ||
                string.Equals(interpolationPath.Confidence, LowConfidence, StringComparison.Ordinal))
            {
                hasUnresolvedSegment = true;
            }
        }

        if (parts.Count == 0)
        {
            return null;
        }

        if (hasUnresolvedSegment && !notes.Contains(UnresolvedSegmentNote, StringComparer.Ordinal))
        {
            notes.Add(UnresolvedSegmentNote);
        }

        var path = string.Concat(parts);
        var hasLiteralContext = !string.Equals(path, "{expr}", StringComparison.Ordinal);
        return new PathResolution(
            Path: path,
            Confidence: hasUnresolvedSegment
                ? (hasLiteralContext ? MediumConfidence : LowConfidence)
                : HighConfidence,
            Notes: notes);
    }

    private static int GetConfidenceRank(string confidence) =>
        confidence switch
        {
            HighConfidence => 2,
            MediumConfidence => 1,
            LowConfidence => 0,
            _ => -1
        };

    private static PathResolution CreateLowConfidencePath() =>
        new("{expr}", LowConfidence, [UnresolvedPathNote]);

    private enum BindPathSource
    {
        Literal,
        GetSection
    }

    private sealed record ScopePreset(
        IReadOnlyList<string> IncludePatterns,
        IReadOnlyList<string> ExcludePatterns);

    private sealed record PathResolution(string Path, string Confidence, IReadOnlyList<string> Notes);
    private sealed record BindPath(PathResolution PathResolution, BindPathSource Source);
    private sealed record DiscoveryMatch(
        string Path,
        string Confidence,
        string SuggestedType,
        IReadOnlyList<string> Notes,
        DiscoveryEvidence Evidence);
}
