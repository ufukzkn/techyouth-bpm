using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;

namespace TechYouthBpm.Application.Services;

public interface IProcessDefinitionService
{
    Task<IReadOnlyList<ProcessDefinitionSummaryDto>> ListAsync(
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RunnableProcessDefinitionDto>> ListRunnableAsync(
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<ProcessDefinitionDto?> GetAsync(
        Guid id,
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<Result<ProcessDefinitionDto>> CreateAsync(
        CreateProcessDefinitionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<Result<ProcessDefinitionDto>> UpdateAsync(
        Guid id,
        UpdateProcessDefinitionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<Result<ProcessDefinitionVersionDto>> CreateVersionAsync(
        Guid processDefinitionId,
        CreateProcessDefinitionVersionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<Result<ProcessDefinitionVersionDto>> UpdateVersionAsync(
        Guid processDefinitionId,
        Guid versionId,
        UpdateProcessDefinitionVersionRequest request,
        UserDto user,
        CancellationToken cancellationToken = default);

    Task<Result<ProcessDefinitionVersionDto>> PublishVersionAsync(
        Guid processDefinitionId,
        Guid versionId,
        UserDto user,
        CancellationToken cancellationToken = default);
}
