using Configuard.Cli.Cli;
using Configuard.Cli.Validation;
using static Configuard.Cli.Tests.TestHelpers;

namespace Configuard.Cli.Tests;

public sealed class CommandHandlersTests
{
    private static readonly object CurrentDirectoryLock = new();

    [Fact]
    public void Execute_DiffWithoutTwoEnvironments_ReturnsInputError()
    {
        var command = new ParsedCommand(
            Name: "diff",
            ContractPath: null,
            Environments: ["staging"],
            OutputFormat: null,
                Verbosity: null,
            Key: null);

        var code = CommandHandlers.Execute(command);

        Assert.Equal(ExitCodes.InputError, code);
    }

    [Fact]
    public void Execute_ExplainUnknownContractKey_ReturnsKeyNotFound()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var appsettingsPath = Path.Combine(tempDir, "appsettings.json");
            var appsettingsEnvPath = Path.Combine(tempDir, "appsettings.staging.json");
            var appsettingsPattern = Path.Combine(tempDir, "appsettings.{env}.json");
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");

            File.WriteAllText(appsettingsPath, """
            {
              "Api": {
                "Key": "abc-123"
              }
            }
            """);
            File.WriteAllText(appsettingsEnvPath, "{}");

            File.WriteAllText(contractPath, $$"""
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "{{EscapeJsonPath(appsettingsPath)}}",
                  "environmentPattern": "{{EscapeJsonPath(appsettingsPattern)}}"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string"
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "explain",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: null,
                Key: "Api:Unknown");

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.KeyNotFound, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ValidateUnsupportedFormat_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var appsettingsPath = Path.Combine(tempDir, "appsettings.json");
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");

            File.WriteAllText(appsettingsPath, """
            {
              "Api": {
                "Key": "abc-123"
              }
            }
            """);

            File.WriteAllText(contractPath, $$"""
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "{{EscapeJsonPath(appsettingsPath)}}",
                  "environmentPattern": "{{EscapeJsonPath(appsettingsPath)}}"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "validate",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "xml",
                Verbosity: null,
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ValidateSarifFormat_IsAccepted()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var appsettingsPath = Path.Combine(tempDir, "appsettings.json");
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");

            File.WriteAllText(appsettingsPath, """
            {
              "Api": {
                "Key": "abc-123"
              }
            }
            """);

            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "validate",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "sarif",
                Verbosity: null,
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.Success, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ValidateUsesDefaultContractPath_WhenFileExistsInCurrentDirectory()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var appsettingsPath = Path.Combine(tempDir, "appsettings.json");
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");

            File.WriteAllText(appsettingsPath, """
            {
              "Api": {
                "Key": "abc-123"
              }
            }
            """);

            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "validate",
                ContractPath: null,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: null,
                Key: null);

            int code;
            lock (CurrentDirectoryLock)
            {
                var original = Environment.CurrentDirectory;
                try
                {
                    Environment.CurrentDirectory = tempDir;
                    code = CommandHandlers.Execute(command);
                }
                finally
                {
                    Environment.CurrentDirectory = original;
                }
            }

            Assert.Equal(ExitCodes.Success, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ValidateResolvesAppSettingsRelativeToContractLocation()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var exampleDir = Path.Combine(tempDir, "examples", "quickstart");
            Directory.CreateDirectory(exampleDir);

            File.WriteAllText(Path.Combine(exampleDir, "appsettings.json"), """
            {
              "Api": {
                "Key": "abc-123"
              }
            }
            """);

            var contractPath = Path.Combine(exampleDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "validate",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: null,
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.Success, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ValidateUnsupportedVerbosity_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var appsettingsPath = Path.Combine(tempDir, "appsettings.json");
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");

            File.WriteAllText(appsettingsPath, """
            {
              "Api": {
                "Key": "abc-123"
              }
            }
            """);

            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "validate",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: "loud",
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ValidateMissingRequiredDotEnvSource_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");

            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                },
                "dotenv": {
                  "base": ".env",
                  "environmentPattern": ".env.{env}",
                  "optional": false
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "validate",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ExplainMissingRequiredEnvSnapshot_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                },
                "envSnapshot": {
                  "environmentPattern": "snapshots/{env}.json",
                  "optional": false
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string"
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "explain",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: "Api:Key");

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_DiffMissingRequiredDotEnvSource_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging", "production"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                },
                "dotenv": {
                  "base": ".env",
                  "environmentPattern": ".env.{env}",
                  "optional": false
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string"
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "diff",
                ContractPath: contractPath,
                Environments: ["staging", "production"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_DiffMalformedEnvSnapshot_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var snapshotsDir = Path.Combine(tempDir, "snapshots");
            Directory.CreateDirectory(snapshotsDir);
            File.WriteAllText(Path.Combine(snapshotsDir, "staging.json"), "{ invalid json");
            File.WriteAllText(Path.Combine(snapshotsDir, "production.json"), """
            {
              "Api": {
                "Key": "ok"
              }
            }
            """);

            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging", "production"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                },
                "envSnapshot": {
                  "environmentPattern": "snapshots/{env}.json",
                  "optional": true
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string"
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "diff",
                ContractPath: contractPath,
                Environments: ["staging", "production"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ValidateMissingRequiredAppSettingsBase_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "validate",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ValidateRejectsAppSettingsPathTraversal_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractDir = Path.Combine(tempDir, "contract");
            Directory.CreateDirectory(contractDir);
            var contractPath = Path.Combine(contractDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "../appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "validate",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ValidateInvalidSourcePreferenceInContract_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "sourcePreference": ["custom"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "validate",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_DiffInvalidSourcePreferenceInContract_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging", "production"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "sourcePreference": ["custom"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "diff",
                ContractPath: contractPath,
                Environments: ["staging", "production"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ExplainInvalidSourcePreferenceInContract_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "sourcePreference": ["custom"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "explain",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: "Api:Key");

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_DiffUsesEnvSnapshotSourcePreferenceAndDetectsChange_ReturnsPolicyFailure()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var snapshotsDir = Path.Combine(tempDir, "snapshots");
            Directory.CreateDirectory(snapshotsDir);
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Api": {
                "Key": "from-appsettings"
              }
            }
            """);
            File.WriteAllText(Path.Combine(snapshotsDir, "staging.json"), """
            {
              "Api": {
                "Key": "from-snapshot-staging"
              }
            }
            """);
            File.WriteAllText(Path.Combine(snapshotsDir, "production.json"), """
            {
              "Api": {
                "Key": "from-snapshot-production"
              }
            }
            """);

            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging", "production"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                },
                "envSnapshot": {
                  "environmentPattern": "snapshots/{env}.json",
                  "optional": true
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "sourcePreference": ["envsnapshot", "appsettings"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "diff",
                ContractPath: contractPath,
                Environments: ["staging", "production"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.PolicyFailure, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_ExplainSourcePreferenceAppSettingsOnly_WhenOnlySnapshotHasValue_ReturnsPolicyFailure()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var snapshotsDir = Path.Combine(tempDir, "snapshots");
            Directory.CreateDirectory(snapshotsDir);
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), "{}");
            File.WriteAllText(Path.Combine(snapshotsDir, "staging.json"), """
            {
              "Api": {
                "Key": "from-snapshot"
              }
            }
            """);

            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                },
                "envSnapshot": {
                  "environmentPattern": "snapshots/{env}.json",
                  "optional": true
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging"],
                  "sourcePreference": ["appsettings"]
                }
              ]
            }
            """);

            var command = new ParsedCommand(
                Name: "explain",
                ContractPath: contractPath,
                Environments: ["staging"],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: "Api:Key");

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.PolicyFailure, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_DiscoverApply_AddsOnlyHighConfidenceFindings_ReturnsSuccess()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Existing:Key",
                  "type": "string"
                }
              ]
            }
            """);

            File.WriteAllText(Path.Combine(tempDir, "sample.cs"), """
            using Microsoft.Extensions.Configuration;

            public class Sample
            {
                public void Configure(IConfiguration configuration, string suffix)
                {
                    var one = configuration["Api:Key"];
                    var two = configuration.GetValue<string>("Dyn:" + suffix);
                }
            }
            """);

            var command = new ParsedCommand(
                Name: "discover",
                ContractPath: contractPath,
                Environments: [],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null,
                ScanPath: tempDir,
                Apply: true);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.Success, code);
            var loaded = ContractLoader.TryLoad(contractPath, out var contract, out var loadError);
            Assert.True(loaded, loadError);
            Assert.NotNull(contract);
            Assert.Contains(contract!.Keys, key => key.Path == "Existing:Key");
            Assert.Contains(contract.Keys, key => key.Path == "Api:Key");
            Assert.Contains(contract.Keys, key => key.Path == "Api:Key" && key.Type == "string");
            Assert.DoesNotContain(contract.Keys, key => key.Path == "Dyn:{expr}");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_DiscoverApply_DoesNotDuplicateAliasMatchedKeys_ReturnsSuccess()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Canonical:ApiKey",
                  "aliases": ["Api:Key"],
                  "type": "string"
                }
              ]
            }
            """);

            File.WriteAllText(Path.Combine(tempDir, "sample.cs"), """
            using Microsoft.Extensions.Configuration;
            public class Sample { public void Run(IConfiguration c) { var x = c["Api:Key"]; } }
            """);

            var command = new ParsedCommand(
                Name: "discover",
                ContractPath: contractPath,
                Environments: [],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null,
                ScanPath: tempDir,
                Apply: true);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.Success, code);
            var loaded = ContractLoader.TryLoad(contractPath, out var contract, out var loadError);
            Assert.True(loaded, loadError);
            Assert.NotNull(contract);
            Assert.Single(contract!.Keys);
            Assert.Equal("Canonical:ApiKey", contract.Keys[0].Path);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_DiscoverApply_UsesInferredTypeFromGetValueGeneric_ReturnsSuccess()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Existing:Key",
                  "type": "string"
                }
              ]
            }
            """);

            File.WriteAllText(Path.Combine(tempDir, "sample.cs"), """
            using Microsoft.Extensions.Configuration;
            public class Sample
            {
                public int Run(IConfiguration c) => c.GetValue<int>("Limits:MaxRetries");
            }
            """);

            var command = new ParsedCommand(
                Name: "discover",
                ContractPath: contractPath,
                Environments: [],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null,
                ScanPath: tempDir,
                Apply: true);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.Success, code);
            var loaded = ContractLoader.TryLoad(contractPath, out var contract, out var loadError);
            Assert.True(loaded, loadError);
            Assert.Contains(contract!.Keys, key => key.Path == "Limits:MaxRetries" && key.Type == "int");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_DiscoverUnknownPreset_ReturnsInputError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var command = new ParsedCommand(
                Name: "discover",
                ContractPath: null,
                Environments: [],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null,
                ScanPath: tempDir,
                ScopePreset: "custom");

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.InputError, code);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_DiscoverWritesReportFile_ReturnsSuccess()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "sample.cs"), @"using Microsoft.Extensions.Configuration;

public class Sample
{
    public void Configure(IConfiguration configuration)
    {
        var key = configuration[""Api:Key""];
    }
}");

            var outputPath = Path.Combine(tempDir, "discover.json");
            var command = new ParsedCommand(
                Name: "discover",
                ContractPath: null,
                Environments: [],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null,
                ScanPath: tempDir,
                OutputPath: outputPath);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.Success, code);
            Assert.True(File.Exists(outputPath));
            var report = File.ReadAllText(outputPath);
            Assert.Contains("\"findings\"", report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"Api:Key\"", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Execute_DiscoverIncludeExcludeFilters_AffectFindings()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var apiDir = Path.Combine(tempDir, "Api");
            var ignoredDir = Path.Combine(tempDir, "Ignored");
            Directory.CreateDirectory(apiDir);
            Directory.CreateDirectory(ignoredDir);

            File.WriteAllText(Path.Combine(apiDir, "ApiSettings.cs"), @"using Microsoft.Extensions.Configuration;
public class ApiSettings { public void Run(IConfiguration c) { var x = c[""Api:Key""]; } }");
            File.WriteAllText(Path.Combine(ignoredDir, "IgnoredSettings.cs"), @"using Microsoft.Extensions.Configuration;
public class IgnoredSettings { public void Run(IConfiguration c) { var x = c[""Ignored:Key""]; } }");

            var outputPath = Path.Combine(tempDir, "discover.json");
            var command = new ParsedCommand(
                Name: "discover",
                ContractPath: null,
                Environments: [],
                OutputFormat: "json",
                Verbosity: "quiet",
                Key: null,
                ScanPath: tempDir,
                OutputPath: outputPath,
                IncludePatterns: ["Api/**"],
                ExcludePatterns: ["Ignored/**"]);

            var code = CommandHandlers.Execute(command);

            Assert.Equal(ExitCodes.Success, code);
            var report = File.ReadAllText(outputPath);
            Assert.Contains("\"Api:Key\"", report, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Ignored:Key\"", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

}
