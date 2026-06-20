using TechYouthBpm.Application.Common;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Application.Workflow;

public class ProcessStateMachine
{
    private static readonly Dictionary<(ProcessStatus Status, WorkflowAction Action), ProcessStatus> Transitions = new()
    {
        [(ProcessStatus.Pending, WorkflowAction.Start)] = ProcessStatus.InProgress,
        [(ProcessStatus.InProgress, WorkflowAction.Approve)] = ProcessStatus.Completed,
        [(ProcessStatus.InProgress, WorkflowAction.Reject)] = ProcessStatus.Rejected
    };

    public Result<ProcessStatus> Move(ProcessStatus currentStatus, WorkflowAction action)
    {
        if (Transitions.TryGetValue((currentStatus, action), out var nextStatus))
        {
            return Result<ProcessStatus>.Success(nextStatus);
        }

        return Result<ProcessStatus>.Failure($"Action {action} is not allowed while process is {currentStatus}.");
    }

    public IReadOnlyList<WorkflowAction> GetAvailableActions(ProcessStatus status)
    {
        return Transitions
            .Where(item => item.Key.Status == status)
            .Select(item => item.Key.Action)
            .ToArray();
    }
}
