using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;

namespace TechYouthBpm.Application.Services;

public interface ITaskService
{
    Task<IReadOnlyList<ProcessTaskDto>> ListMyTasksAsync(UserDto user, CancellationToken cancellationToken = default);
    Task<Result<ProcessDetailDto>> ExecuteActionAsync(Guid taskId, TaskActionRequest request, UserDto user, CancellationToken cancellationToken = default);
}
