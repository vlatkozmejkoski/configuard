using System.Text.Json;

namespace Configuard.Cli.Validation;

internal static class ContractConstraintRulesValidator
{
    public static bool TryValidate(string keyPath, JsonElement constraints, out string? error)
    {
        error = null;
        if (constraints.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return true;
        }

        if (constraints.ValueKind != JsonValueKind.Object)
        {
            error = $"Key '{keyPath}' has invalid 'constraints' shape. Expected an object.";
            return false;
        }

        if (!TryValidateBoundPair(keyPath, constraints, "minLength", "maxLength", integersOnly: true, out error))
        {
            return false;
        }

        if (!TryValidateNonNegativeIntegerConstraint(keyPath, constraints, "minLength", out error) ||
            !TryValidateNonNegativeIntegerConstraint(keyPath, constraints, "maxLength", out error) ||
            !TryValidateNonNegativeIntegerConstraint(keyPath, constraints, "minItems", out error) ||
            !TryValidateNonNegativeIntegerConstraint(keyPath, constraints, "maxItems", out error))
        {
            return false;
        }

        if (!TryValidateBoundPair(keyPath, constraints, "minimum", "maximum", integersOnly: false, out error))
        {
            return false;
        }

        if (!TryValidateBoundPair(keyPath, constraints, "minItems", "maxItems", integersOnly: true, out error))
        {
            return false;
        }

        if (constraints.TryGetProperty("enum", out var enumElement))
        {
            if (enumElement.ValueKind != JsonValueKind.Array)
            {
                error = $"Key '{keyPath}' has invalid 'enum' constraint. Expected an array.";
                return false;
            }

            if (enumElement.GetArrayLength() == 0)
            {
                error = $"Key '{keyPath}' has invalid 'enum' constraint. Array must not be empty.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateBoundPair(
        string keyPath,
        JsonElement constraints,
        string minProperty,
        string maxProperty,
        bool integersOnly,
        out string? error)
    {
        error = null;
        if (!constraints.TryGetProperty(minProperty, out var minElement) ||
            !constraints.TryGetProperty(maxProperty, out var maxElement))
        {
            return true;
        }

        if (integersOnly)
        {
            if (!minElement.TryGetInt32(out var minInt) || !maxElement.TryGetInt32(out var maxInt))
            {
                error = $"Key '{keyPath}' has non-integer '{minProperty}'/'{maxProperty}' values.";
                return false;
            }

            if (minInt > maxInt)
            {
                error = $"Key '{keyPath}' has invalid constraints: '{minProperty}' cannot be greater than '{maxProperty}'.";
                return false;
            }

            return true;
        }

        if (!minElement.TryGetDouble(out var minDouble) || !maxElement.TryGetDouble(out var maxDouble))
        {
            error = $"Key '{keyPath}' has non-numeric '{minProperty}'/'{maxProperty}' values.";
            return false;
        }

        if (minDouble > maxDouble)
        {
            error = $"Key '{keyPath}' has invalid constraints: '{minProperty}' cannot be greater than '{maxProperty}'.";
            return false;
        }

        return true;
    }

    private static bool TryValidateNonNegativeIntegerConstraint(
        string keyPath,
        JsonElement constraints,
        string propertyName,
        out string? error)
    {
        error = null;
        if (!constraints.TryGetProperty(propertyName, out var element))
        {
            return true;
        }

        if (!element.TryGetInt32(out var value))
        {
            error = $"Key '{keyPath}' has invalid '{propertyName}' constraint. Expected an integer.";
            return false;
        }

        if (value < 0)
        {
            error = $"Key '{keyPath}' has invalid '{propertyName}' constraint. Value must be >= 0.";
            return false;
        }

        return true;
    }
}
