using TechYouthBpm.Application.Auth;
using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;
using TechYouthBpm.Domain.Entities;

namespace TechYouthBpm.Application.Services;

public interface IWorkflowVisibilityService
{
    Result<WorkflowVisibilityScope> ResolveScope(string? requestedScope, UserDto user);

    IQueryable<ProcessInstance> ApplyProcessScope(
        IQueryable<ProcessInstance> query,
        UserDto user,
        WorkflowVisibilityScope scope);

    IQueryable<ProcessTask> ApplyTaskScope(
        IQueryable<ProcessTask> query,
        UserDto user,
        WorkflowVisibilityScope scope);

    bool CanViewProcess(ProcessInstance process, UserDto user);
}
