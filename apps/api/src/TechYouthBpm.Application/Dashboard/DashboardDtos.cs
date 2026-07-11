namespace TechYouthBpm.Application.Dashboard;

public record DashboardSummaryDto(
    int OpenTaskCount,
    int InProgressProcessCount,
    int CompletedProcessCount);
