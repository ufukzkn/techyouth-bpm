using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Forms;

namespace TechYouthBpm.Application.Services;

public interface IFormVersionService
{
    Task<IReadOnlyList<FormDefinitionVersionDto>> ListVersionsAsync(
        Guid formDefinitionId,
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<FormDefinitionVersionDto?> GetVersionAsync(
        Guid formDefinitionId,
        Guid versionId,
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<Result<FormDefinitionVersionDto>> CreateDraftAsync(
        Guid formDefinitionId,
        CreateFormVersionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<Result<FormDefinitionVersionDto>> UpdateAsync(
        Guid formDefinitionId,
        Guid versionId,
        UpdateFormVersionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<Result<FormDefinitionVersionDto>> PublishAsync(
        Guid formDefinitionId,
        Guid versionId,
        UserDto user,
        CancellationToken cancellationToken = default);
}
