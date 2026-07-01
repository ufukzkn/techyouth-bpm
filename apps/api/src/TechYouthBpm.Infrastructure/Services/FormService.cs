using Microsoft.EntityFrameworkCore;
using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Services;
using TechYouthBpm.Domain.Entities;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Data;

namespace TechYouthBpm.Infrastructure.Services;

public class FormService(AppDbContext db) : IFormService
{
    public async Task<IReadOnlyList<FormDefinitionDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var forms = await FormQuery()
            .OrderByDescending(form => form.CreatedAt)
            .ToListAsync(cancellationToken);

        return forms.Select(form => form.ToDto()).ToArray();
    }

    public async Task<FormDefinitionDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var form = await FormQuery().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return form?.ToDto();
    }

    public async Task<Result<FormDefinitionDto>> CreateAsync(CreateFormRequest request, UserDto user, CancellationToken cancellationToken = default)
    {
        if (user.Role != Role.Admin)
        {
            return Result<FormDefinitionDto>.Failure("Only Admin users can create form definitions.");
        }

        var errors = ValidateDefinition(request);
        if (errors.Count > 0)
        {
            return Result<FormDefinitionDto>.Failure(errors);
        }

        var form = new FormDefinition
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
        };
        form.Fields = BuildFields(request, form.Id);

        db.FormDefinitions.Add(form);
        await db.SaveChangesAsync(cancellationToken);

        var saved = await GetAsync(form.Id, cancellationToken);
        return Result<FormDefinitionDto>.Success(saved!);
    }

    public async Task<Result<FormDefinitionDto>> UpdateAsync(
        Guid id,
        CreateFormRequest request,
        UserDto user,
        CancellationToken cancellationToken = default)
    {
        if (user.Role != Role.Admin)
        {
            return Result<FormDefinitionDto>.Failure("Only Admin users can update form definitions.");
        }

        var errors = ValidateDefinition(request);
        if (errors.Count > 0)
        {
            return Result<FormDefinitionDto>.Failure(errors);
        }

        var form = await FormQuery().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (form is null)
        {
            return Result<FormDefinitionDto>.Failure("Form definition was not found.");
        }

        var formId = form.Id;
        form.Name = request.Name.Trim();
        form.Description = request.Description.Trim();

        var oldFields = form.Fields.ToList();
        db.FieldValidationRules.RemoveRange(oldFields.SelectMany(field => field.ValidationRules));
        db.FormFieldDefinitions.RemoveRange(oldFields);
        form.Fields.Clear();

        await db.SaveChangesAsync(cancellationToken);

        db.ChangeTracker.Clear();
        db.FormFieldDefinitions.AddRange(BuildFields(request, formId));

        await db.SaveChangesAsync(cancellationToken);

        var saved = await GetAsync(formId, cancellationToken);
        return Result<FormDefinitionDto>.Success(saved!);
    }

    private IQueryable<FormDefinition> FormQuery() =>
        db.FormDefinitions
            .Include(form => form.Fields)
            .ThenInclude(field => field.ValidationRules);

    private static List<FormFieldDefinition> BuildFields(CreateFormRequest request, Guid formDefinitionId) =>
        request.Fields
            .OrderBy(field => field.SortOrder)
            .Select((field, index) => new FormFieldDefinition
            {
                Id = Guid.NewGuid(),
                FormDefinitionId = formDefinitionId,
                Key = field.Key.Trim(),
                Label = field.Label.Trim(),
                Type = field.Type,
                Required = field.Required,
                SortOrder = index + 1,
                OptionsJson = JsonHelpers.Serialize(field.Options),
                ValidationRules = field.ValidationRules.Select(rule => new FieldValidationRule
                {
                    Id = Guid.NewGuid(),
                    RuleType = rule.RuleType,
                    DependsOnFieldKey = rule.DependsOnFieldKey.Trim(),
                    ExpectedValue = rule.ExpectedValue.Trim(),
                    Message = rule.Message.Trim()
                }).ToList()
            })
            .ToList();

    private static List<string> ValidateDefinition(CreateFormRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Form name is required.");
        }

        if (request.Fields.Count == 0)
        {
            errors.Add("At least one form field is required.");
        }

        var duplicateKeys = request.Fields
            .GroupBy(field => field.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var key in duplicateKeys)
        {
            errors.Add($"Field key '{key}' is duplicated.");
        }

        foreach (var field in request.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key))
            {
                errors.Add("Every field needs a key.");
            }

            if (string.IsNullOrWhiteSpace(field.Label))
            {
                errors.Add($"Field '{field.Key}' needs a label.");
            }

            if (field.Type == FieldType.Select && field.Options.Count == 0)
            {
                errors.Add($"Select field '{field.Key}' needs options.");
            }
        }

        return errors;
    }
}
