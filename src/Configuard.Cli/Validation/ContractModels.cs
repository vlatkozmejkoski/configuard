using System.Text.Json;
using System.Text.Json.Serialization;

namespace Configuard.Cli.Validation;

internal sealed class ContractDocument
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("environments")]
    public List<string> Environments { get; init; } = [];

    [JsonPropertyName("sources")]
    public ContractSources Sources { get; init; } = new();

    [JsonPropertyName("keys")]
    public List<ContractKeyRule> Keys { get; init; } = [];
}

internal sealed class ContractSources
{
    [JsonPropertyName("appsettings")]
    public AppSettingsSource? AppSettings { get; init; }

    [JsonPropertyName("dotenv")]
    public DotEnvSource? DotEnv { get; init; }

    [JsonPropertyName("envSnapshot")]
    public EnvSnapshotSource? EnvSnapshot { get; init; }
}

internal sealed class AppSettingsSource
{
    [JsonPropertyName("base")]
    public string? Base { get; init; }

    [JsonPropertyName("environmentPattern")]
    public string? EnvironmentPattern { get; init; }
}

internal sealed class DotEnvSource
{
    [JsonPropertyName("base")]
    public string? Base { get; init; }

    [JsonPropertyName("environmentPattern")]
    public string? EnvironmentPattern { get; init; }

    [JsonPropertyName("optional")]
    public bool Optional { get; init; }
}

internal sealed class EnvSnapshotSource
{
    [JsonPropertyName("environmentPattern")]
    public string? EnvironmentPattern { get; init; }

    [JsonPropertyName("optional")]
    public bool Optional { get; init; }
}

internal sealed class ContractKeyRule
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; init; } = [];

    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    [JsonPropertyName("requiredIn")]
    public List<string> RequiredIn { get; init; } = [];

    [JsonPropertyName("forbiddenIn")]
    public List<string> ForbiddenIn { get; init; } = [];

    [JsonPropertyName("sensitive")]
    public bool Sensitive { get; init; }

    [JsonPropertyName("sourcePreference")]
    public List<string> SourcePreference { get; init; } = [];

    [JsonPropertyName("constraints")]
    public JsonElement Constraints { get; init; }
}
