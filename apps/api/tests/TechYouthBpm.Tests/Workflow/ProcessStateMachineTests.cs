using TechYouthBpm.Application.Workflow;
using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Tests.Workflow;

public class ProcessStateMachineTests
{
    private readonly ProcessStateMachine _stateMachine = new();

    [Theory]
    [InlineData(ProcessStatus.Pending, WorkflowAction.Start, ProcessStatus.InProgress)]
    [InlineData(ProcessStatus.InProgress, WorkflowAction.Approve, ProcessStatus.Completed)]
    [InlineData(ProcessStatus.InProgress, WorkflowAction.Reject, ProcessStatus.Rejected)]
    public void Move_Allows_Defined_Transitions(
        ProcessStatus currentStatus,
        WorkflowAction action,
        ProcessStatus expectedStatus)
    {
        var result = _stateMachine.Move(currentStatus, action);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedStatus, result.Value);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(ProcessStatus.Pending, WorkflowAction.Approve)]
    [InlineData(ProcessStatus.Pending, WorkflowAction.Reject)]
    [InlineData(ProcessStatus.Completed, WorkflowAction.Approve)]
    [InlineData(ProcessStatus.Rejected, WorkflowAction.Start)]
    public void Move_Rejects_Undefined_Transitions(ProcessStatus currentStatus, WorkflowAction action)
    {
        var result = _stateMachine.Move(currentStatus, action);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("not allowed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetAvailableActions_Returns_Only_Actions_For_Current_Status()
    {
        var pendingActions = _stateMachine.GetAvailableActions(ProcessStatus.Pending);
        var inProgressActions = _stateMachine.GetAvailableActions(ProcessStatus.InProgress);
        var completedActions = _stateMachine.GetAvailableActions(ProcessStatus.Completed);

        Assert.Equal([WorkflowAction.Start], pendingActions);
        Assert.Equal([WorkflowAction.Approve, WorkflowAction.Reject], inProgressActions);
        Assert.Empty(completedActions);
    }
}
