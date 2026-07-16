using TechYouthBpm.Application.Forms;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Infrastructure.Services;

internal static class FormVersionModel
{
    public static IReadOnlyList<string> ValidatePages(IReadOnlyList<CreateFormPageRequest>? pages)
    {
        var errors = new List<string>();
        pages ??= [];

        if (pages.Count == 0)
        {
            errors.Add("At least one form page is required.");
            return errors;
        }

        foreach (var duplicate in pages
                     .GroupBy(page => page.Key?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Page key '{duplicate.Key}' is duplicated.");
        }

        var fields = pages.SelectMany(page => page.Fields ?? []).ToArray();
        if (fields.Length == 0)
        {
            errors.Add("At least one form field is required.");
        }

        foreach (var duplicate in fields
                     .GroupBy(field => field.Key?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Field key '{duplicate.Key}' is duplicated across the form version.");
        }

        var fieldKeys = fields
            .Select(field => field.Key?.Trim() ?? string.Empty)
            .Where(key => key.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Key))
            {
                errors.Add("Every page needs a key.");
            }

            if (string.IsNullOrWhiteSpace(page.Title))
            {
                errors.Add($"Page '{page.Key}' needs a title.");
            }

            foreach (var field in page.Fields ?? [])
            {
                if (string.IsNullOrWhiteSpace(field.Key))
                {
                    errors.Add($"Every field on page '{page.Key}' needs a key.");
                }

                if (string.IsNullOrWhiteSpace(field.Label))
                {
                    errors.Add($"Field '{field.Key}' needs a label.");
                }

                if (field.Type is FieldType.Select or FieldType.Radio)
                {
                    var options = field.Options ?? [];
                    if (options.Count == 0)
                    {
                        errors.Add($"Option field '{field.Key}' needs options.");
                    }
                    else if (options.Any(string.IsNullOrWhiteSpace))
                    {
                        errors.Add($"Option field '{field.Key}' cannot contain empty options.");
                    }
                    else if (options
                             .GroupBy(option => option.Trim(), StringComparer.OrdinalIgnoreCase)
                             .Any(group => group.Count() > 1))
                    {
                        errors.Add($"Option field '{field.Key}' cannot contain duplicate options.");
                    }
                }

                foreach (var rule in field.ValidationRules ?? [])
                {
                    if (rule.RuleType == ValidationRuleType.RequiredWhen
                        && !fieldKeys.Contains(rule.DependsOnFieldKey?.Trim() ?? string.Empty))
                    {
                        errors.Add($"Validation rule on '{field.Key}' references unknown field '{rule.DependsOnFieldKey}'.");
                    }
                }
            }
        }

        return errors;
    }

    public static List<FormPageDefinition> BuildPages(IReadOnlyList<CreateFormPageRequest> pages) =>
        pages
            .OrderBy(page => page.SortOrder)
            .Select((page, pageIndex) => new FormPageDefinition
            {
                Id = Guid.NewGuid(),
                Key = page.Key.Trim(),
                Title = page.Title.Trim(),
                Description = page.Description?.Trim() ?? string.Empty,
                SortOrder = pageIndex + 1,
                Fields = (page.Fields ?? [])
                    .OrderBy(field => field.SortOrder)
                    .Select((field, fieldIndex) => BuildField(field, fieldIndex + 1))
                    .ToList()
            })
            .ToList();

    public static FormDefinitionVersion BuildLegacyPublishedVersion(
        FormDefinition form,
        int versionNumber,
        Guid actorUserId,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = form.Id,
            VersionNumber = versionNumber,
            Status = DefinitionVersionStatus.Published,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            PublishedByUserId = actorUserId,
            PublishedAt = now,
            Pages =
            [
                new FormPageDefinition
                {
                    Id = Guid.NewGuid(),
                    Key = "main",
                    Title = form.Name,
                    Description = form.Description,
                    SortOrder = 1,
                    Fields = form.Fields
                        .OrderBy(field => field.SortOrder)
                        .Select(field => new FormVersionFieldDefinition
                        {
                            Id = Guid.NewGuid(),
                            Key = field.Key,
                            Label = field.Label,
                            Type = field.Type,
                            Required = field.Required,
                            SortOrder = field.SortOrder,
                            OptionsJson = field.OptionsJson,
                            ValidationRules = field.ValidationRules.Select(rule => new FormVersionFieldValidationRule
                            {
                                Id = Guid.NewGuid(),
                                RuleType = rule.RuleType,
                                DependsOnFieldKey = rule.DependsOnFieldKey,
                                ExpectedValue = rule.ExpectedValue,
                                Message = rule.Message
                            }).ToList()
                        })
                        .ToList()
                }
            ]
        };

    private static FormVersionFieldDefinition BuildField(CreateFormFieldRequest field, int sortOrder) =>
        new()
        {
            Id = Guid.NewGuid(),
            Key = field.Key.Trim(),
            Label = field.Label.Trim(),
            Type = field.Type,
            Required = field.Required,
            SortOrder = sortOrder,
            OptionsJson = JsonHelpers.Serialize((field.Options ?? []).Select(option => option.Trim()).ToArray()),
            ValidationRules = (field.ValidationRules ?? []).Select(rule => new FormVersionFieldValidationRule
            {
                Id = Guid.NewGuid(),
                RuleType = rule.RuleType,
                DependsOnFieldKey = rule.DependsOnFieldKey?.Trim() ?? string.Empty,
                ExpectedValue = rule.ExpectedValue?.Trim() ?? string.Empty,
                Message = rule.Message?.Trim() ?? string.Empty
            }).ToList()
        };
}
