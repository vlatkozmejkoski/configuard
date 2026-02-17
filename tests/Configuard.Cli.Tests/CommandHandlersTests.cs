using Configuard.Cli.Cli;
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

}
