using TechYouthBpm.Domain.Enums;

namespace TechYouthBpm.Application.Auth;

public record LoginRequest(string Username, string Password);

public record UserDto(Guid Id, string Username, string DisplayName, Role Role);

public record LoginResponse(string Token, UserDto User, DateTime ExpiresAt);
