using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Processes;

namespace TechYouthBpm.Application.Services;

public interface IProcessGraphValidator
{
    Result ValidateStructure(ProcessGraphDto graph);

    Task<Result> ValidateForPublishAsync(
        ProcessGraphDto graph,
        Guid communityId,
        Guid formDefinitionVersionId,
        CancellationToken cancellationToken = default);
}
