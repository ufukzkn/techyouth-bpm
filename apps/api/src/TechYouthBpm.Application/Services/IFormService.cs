using TechYouthBpm.Application.Common;
using TechYouthBpm.Application.Forms;
using TechYouthBpm.Application.Auth;

namespace TechYouthBpm.Application.Services;

public interface IFormService
{
    Task<IReadOnlyList<FormDefinitionDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<FormDefinitionDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<FormDefinitionDto>> CreateAsync(CreateFormRequest request, UserDto user, CancellationToken cancellationToken = default);
}
