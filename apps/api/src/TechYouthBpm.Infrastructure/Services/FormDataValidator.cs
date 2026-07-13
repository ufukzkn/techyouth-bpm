using System.Globalization;
using System.Text.Json;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Infrastructure.Services;

internal static class FormDataValidator
{
    public static IReadOnlyList<string> Validate(FormDefinitionDto form, JsonElement formData)
    {
        var errors = new List<string>();

        foreach (var field in form.Fields)
        {
            var hasValue = TryGetValue(formData, field.Key, out var value) && !IsEmpty(value);

            if (field.Required && !hasValue)
            {
                errors.Add($"{field.Label} is required.");
                continue;
            }

            foreach (var rule in field.ValidationRules.Where(rule => rule.RuleType == ValidationRuleType.RequiredWhen))
            {
                if (TryGetValue(formData, rule.DependsOnFieldKey, out var dependsOnValue)
                    && ToComparable(dependsOnValue) == rule.ExpectedValue
                    && !hasValue)
                {
                    errors.Add(string.IsNullOrWhiteSpace(rule.Message) ? $"{field.Label} is required." : rule.Message);
                }
            }

            if (hasValue)
            {
                ValidateType(field, value, errors);
            }
        }

        return errors;
    }

    private static bool TryGetValue(JsonElement data, string key, out JsonElement value)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(key, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool IsEmpty(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null => true,
            JsonValueKind.Undefined => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            _ => false
        };

    private static string ToComparable(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            _ => value.GetRawText()
        };

    private static void ValidateType(FormFieldDto field, JsonElement value, List<string> errors)
    {
        switch (field.Type)
        {
            case FieldType.Text when value.ValueKind != JsonValueKind.String:
            case FieldType.TextArea when value.ValueKind != JsonValueKind.String:
                errors.Add($"{field.Label} must be text.");
                break;
            case FieldType.Number when value.ValueKind != JsonValueKind.Number:
                errors.Add($"{field.Label} must be a number.");
                break;
            case FieldType.Email when value.ValueKind != JsonValueKind.String || !value.GetString()!.Contains('@'):
                errors.Add($"{field.Label} must be a valid email.");
                break;
            case FieldType.Date when value.ValueKind != JsonValueKind.String
                || !DateOnly.TryParseExact(
                    value.GetString(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _):
                errors.Add($"{field.Label} must be a valid date in yyyy-MM-dd format.");
                break;
            case FieldType.Checkbox when value.ValueKind is not JsonValueKind.True and not JsonValueKind.False:
                errors.Add($"{field.Label} must be true or false.");
                break;
            case FieldType.Select when value.ValueKind != JsonValueKind.String || !field.Options.Contains(value.GetString()):
            case FieldType.Radio when value.ValueKind != JsonValueKind.String || !field.Options.Contains(value.GetString()):
                errors.Add($"{field.Label} must be one of the defined options.");
                break;
        }
    }
}
