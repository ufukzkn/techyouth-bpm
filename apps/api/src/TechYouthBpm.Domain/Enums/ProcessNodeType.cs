namespace TechYouthBpm.Domain.Enums;

public enum ProcessNodeType
{
    Start = 1,
    UserTask = 2,
    ExclusiveGateway = 3,
    CompletedEnd = 4,
    RejectedEnd = 5,
    TeamSwimlane = 6
}
