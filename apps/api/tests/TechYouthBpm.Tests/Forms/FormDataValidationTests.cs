using System.Text.Json;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Enums;
using TechYouthBpm.Infrastructure.Services;

namespace TechYouthBpm.Tests.Forms;

public class FormDataValidationTests
{
    [Fact]
    public async Task StartAsync_Rejects_Date_With_Invalid_Format()
    {
        var result = await StartWithValueAsync(FieldType.Date, "\"13.07.2026\"");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("yyyy-MM-dd", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartAsync_Rejects_Non_String_TextArea()
    {
        var result = await StartWithValueAsync(FieldType.TextArea, "42");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("must be text", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<TechYouthBpm.Application.Common.Result<ProcessDetailDto>> StartWithValueAsync(
        FieldType fieldType,
        string jsonValue)
    {
        await using var db = TestDbFactory.Create();
        var admin = TestDbFactory.SeedUser(db, Role.Admin);
        var adminDto = TestDbFactory.ToDto(admin);
        var formService = new FormService(db);
        var form = await formService.CreateAsync(
            new CreateFormRequest(
                "Validation Form",
                "Validation test form",
                [
                    new CreateFormFieldRequest("value", "Value", fieldType, true, 1, [], [])
                ]),
            adminDto);
        var processService = new ProcessService(
            db,
            formService,
            new ProcessStateMachine(),
            new SystemAuditService(db));
        using var data = JsonDocument.Parse($"{{\"value\":{jsonValue}}}");

        return await processService.StartAsync(
            new StartProcessRequest(form.Value!.Id, data.RootElement.Clone()),
            adminDto);
    }
}
