using System.Net;
using System.Text.Json;

namespace TechYouthBpm.Tests.Integration;

public sealed class OpenApiContractIntegrationTests
{
    private static readonly string[] ExpectedOperations =
    [
        "GET /api/audit/system",
        "GET /api/audit/system/counts",
        "DELETE /api/auth/sessions/{sessionId}",
        "GET /api/auth/me",
        "GET /api/auth/sessions",
        "PATCH /api/auth/me/profile",
        "POST /api/auth/browser-login",
        "POST /api/auth/forgot-password",
        "POST /api/auth/login",
        "POST /api/auth/logout",
        "POST /api/auth/me/email-verification",
        "POST /api/auth/me/email-verification/confirm",
        "POST /api/auth/me/password",
        "POST /api/auth/public-email-verification/confirm",
        "POST /api/auth/public-email-verification/start",
        "POST /api/auth/refresh",
        "POST /api/auth/register",
        "POST /api/auth/reset-password",
        "GET /api/communities",
        "GET /api/communities/role-templates",
        "GET /api/communities/{communityId}/roles",
        "GET /api/communities/{communityId}/summary",
        "GET /api/communities/{communityId}/users",
        "PATCH /api/communities/{communityId}",
        "PATCH /api/communities/{communityId}/invite-code/regenerate",
        "PATCH /api/communities/{communityId}/roles/{roleId}",
        "PATCH /api/communities/{communityId}/users/{userId}/membership",
        "POST /api/communities",
        "POST /api/communities/{communityId}/roles",
        "POST /api/communities/{communityId}/users",
        "DELETE /api/communities/{communityId}/roles/{roleId}",
        "GET /api/dashboard/summary",
        "GET /api/forms",
        "GET /api/forms/{id}",
        "GET /api/forms/{id}/versions",
        "GET /api/forms/{id}/versions/{versionId}",
        "POST /api/forms",
        "POST /api/forms/{id}/versions",
        "POST /api/forms/{id}/versions/{versionId}/archive",
        "POST /api/forms/{id}/versions/{versionId}/publish",
        "PUT /api/forms/{id}",
        "PUT /api/forms/{id}/versions/{versionId}",
        "GET /api/notifications",
        "PATCH /api/notifications/{notificationId}/read",
        "PATCH /api/notifications/{notificationId}/read-state",
        "POST /api/notifications/read-all",
        "GET /api/process-definitions",
        "GET /api/process-definitions/runnable",
        "GET /api/process-definitions/{id}",
        "POST /api/process-definitions",
        "POST /api/process-definitions/{id}/validate",
        "POST /api/process-definitions/{id}/versions",
        "POST /api/process-definitions/{id}/versions/{versionId}/publish",
        "PUT /api/process-definitions/{id}",
        "PUT /api/process-definitions/{id}/versions/{versionId}",
        "GET /api/processes",
        "GET /api/processes/{id}",
        "POST /api/processes/start",
        "POST /api/processes/start-version",
        "POST /api/processes/start/version",
        "DELETE /api/tasks/{id}/claim",
        "GET /api/tasks/my",
        "POST /api/tasks/{id}/actions",
        "POST /api/tasks/{id}/claim",
        "POST /api/tasks/{id}/release",
        "GET /api/teams",
        "GET /api/teams/unassigned/members",
        "GET /api/teams/{teamId}",
        "GET /api/teams/{teamId}/candidates",
        "GET /api/teams/{teamId}/members",
        "GET /api/teams/{teamId}/members/{userId}/tasks",
        "GET /api/teams/{teamId}/roster",
        "PATCH /api/teams/{teamId}",
        "PATCH /api/teams/{teamId}/members/{userId}",
        "POST /api/teams",
        "POST /api/teams/{teamId}/members",
        "DELETE /api/teams/{teamId}/members/{userId}",
        "GET /api/users",
        "GET /api/users/{userId}/sessions",
        "GET /api/users/{userId}/team-memberships",
        "GET /api/users/{userId}/community-transfer-preview",
        "PATCH /api/users/{userId}/access",
        "POST /api/users",
        "POST /api/users/{userId}/password-reset-by-admin",
        "POST /api/users/{userId}/community-transfer",
        "DELETE /api/users/{userId}",
        "DELETE /api/users/{userId}/sessions/{sessionId}",
    ];

    [Fact]
    public async Task Public_controller_contract_matches_the_approved_normalized_snapshot()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actual = document.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(operation => IsHttpMethod(operation.Name))
                .Select(operation => $"{operation.Name.ToUpperInvariant()} {path.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ExpectedOperations.Order(StringComparer.Ordinal),
            actual);
    }

    private static bool IsHttpMethod(string value) =>
        value is "get" or "post" or "put" or "patch" or "delete";
}
