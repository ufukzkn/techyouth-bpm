using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Processes;

namespace TechYouthBpm.Application.Services;

public interface IProcessService
{
    Task<IReadOnlyList<ProcessSummaryDto>> ListAsync(UserDto user, CancellationToken cancellationToken = default);
    Task<ProcessDetailDto?> GetAsync(Guid id, UserDto user, CancellationToken cancellationToken = default);
    Task<Result<ProcessDetailDto>> StartAsync(StartProcessRequest request, UserDto user, CancellationToken cancellationToken = default);
}
