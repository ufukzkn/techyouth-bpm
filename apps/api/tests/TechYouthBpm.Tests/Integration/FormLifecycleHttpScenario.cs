using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TechYouthBpm.Tests.Integration;

internal static class FormLifecycleHttpScenario
{
    public static async Task<FormLifecycleHttpResult> RunAsync(
        HttpClient client,
        string token,
        string namePrefix = "HTTP Form Lifecycle")
    {
        var communityId = await GetActiveCommunityIdAsync(client, token);
        var field = new
        {
            key = "requestTitle",
            label = "Request title",
            type = "Text",
            required = true,
            sortOrder = 0,
            options = Array.Empty<string>(),
            validationRules = Array.Empty<object>()
        };

        using var formRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Post, "/api/forms", token);
        formRequest.Content = JsonContent.Create(new
        {
            name = $"{namePrefix} {Guid.NewGuid():N}",
            description = "Exercises the complete form version lifecycle.",
            communityId,
            createPublishedVersion = false,
            fields = new[] { field }
        });
        using var formResponse = await client.SendAsync(formRequest);
        var form = await ReadRequiredJsonAsync(formResponse, "create form");
        var formId = form.GetProperty("id").GetGuid();

        using var draftRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/forms/{formId}/versions",
            token);
        draftRequest.Content = JsonContent.Create(new
        {
            pages = new[]
            {
                new
                {
                    key = "main",
                    title = "Lifecycle page",
                    description = "Initial draft",
                    sortOrder = 0,
                    fields = new[] { field }
                }
            }
        });
        using var draftResponse = await client.SendAsync(draftRequest);
        var draft = await ReadRequiredJsonAsync(draftResponse, "create draft");
        var versionId = draft.GetProperty("id").GetGuid();

        using var formUpdateRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Put,
            $"/api/forms/{formId}",
            token);
        formUpdateRequest.Content = JsonContent.Create(new
        {
            name = form.GetProperty("name").GetString(),
            description = "Updated through the designer persistence order.",
            communityId,
            createPublishedVersion = false,
            fields = new[] { field }
        });
        using var formUpdateResponse = await client.SendAsync(formUpdateRequest);
        await ReadRequiredJsonAsync(formUpdateResponse, "update form metadata");

        using var updateRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Put,
            $"/api/forms/{formId}/versions/{versionId}",
            token);
        updateRequest.Content = JsonContent.Create(new
        {
            pages = new[]
            {
                new
                {
                    key = "main",
                    title = "Updated lifecycle page",
                    description = "Updated draft",
                    sortOrder = 0,
                    fields = new[]
                    {
                        new
                        {
                            key = "requestTitle",
                            label = "Updated request title",
                            type = "Text",
                            required = true,
                            sortOrder = 0,
                            options = Array.Empty<string>(),
                            validationRules = Array.Empty<object>()
                        }
                    }
                }
            }
        });
        using var updateResponse = await client.SendAsync(updateRequest);
        var updated = await ReadRequiredJsonAsync(updateResponse, "update draft");

        using var publishDraftUpdateRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Put,
            $"/api/forms/{formId}/versions/{versionId}",
            token);
        publishDraftUpdateRequest.Content = JsonContent.Create(new
        {
            pages = new[]
            {
                new
                {
                    key = "main",
                    title = "Updated lifecycle page",
                    description = "Updated before publish",
                    sortOrder = 0,
                    fields = new[]
                    {
                        new
                        {
                            key = "requestTitle",
                            label = "Updated request title",
                            type = "Text",
                            required = true,
                            sortOrder = 0,
                            options = Array.Empty<string>(),
                            validationRules = Array.Empty<object>()
                        }
                    }
                }
            }
        });
        using var publishDraftUpdateResponse = await client.SendAsync(publishDraftUpdateRequest);
        await ReadRequiredJsonAsync(publishDraftUpdateResponse, "update draft before publish");

        using var publishRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/forms/{formId}/versions/{versionId}/publish",
            token);
        using var publishResponse = await client.SendAsync(publishRequest);
        var published = await ReadRequiredJsonAsync(publishResponse, "publish version");

        using var startRequest = IntegrationTestHttp.BearerRequest(HttpMethod.Post, "/api/processes/start", token);
        startRequest.Content = JsonContent.Create(new
        {
            formDefinitionId = formId,
            formData = new { requestTitle = "Lifecycle process request" }
        });
        using var startResponse = await client.SendAsync(startRequest);
        var process = await ReadRequiredJsonAsync(startResponse, "start process");

        using var archiveRequest = IntegrationTestHttp.BearerRequest(
            HttpMethod.Post,
            $"/api/forms/{formId}/versions/{versionId}/archive",
            token);
        using var archiveResponse = await client.SendAsync(archiveRequest);
        var archived = await ReadRequiredJsonAsync(archiveResponse, "archive version");

        return new FormLifecycleHttpResult(
            formResponse.StatusCode,
            draftResponse.StatusCode,
            formUpdateResponse.StatusCode,
            updateResponse.StatusCode,
            publishDraftUpdateResponse.StatusCode,
            publishResponse.StatusCode,
            startResponse.StatusCode,
            archiveResponse.StatusCode,
            updated.GetProperty("pages")[0].GetProperty("title").GetString(),
            published.GetProperty("status").GetString(),
            process.GetProperty("status").GetString(),
            archived.GetProperty("status").GetString());
    }

    private static async Task<Guid> GetActiveCommunityIdAsync(HttpClient client, string token)
    {
        using var request = IntegrationTestHttp.BearerRequest(HttpMethod.Get, "/api/communities", token);
        using var response = await client.SendAsync(request);
        var communities = await ReadRequiredJsonAsync(response, "list communities");
        return communities.EnumerateArray()
            .First(item => item.GetProperty("isActive").GetBoolean())
            .GetProperty("id")
            .GetGuid();
    }

    private static async Task<JsonElement> ReadRequiredJsonAsync(HttpResponseMessage response, string operation)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to {operation}: {(int)response.StatusCode} {response.StatusCode}. {content}");
        }

        return JsonSerializer.Deserialize<JsonElement>(content);
    }
}

internal sealed record FormLifecycleHttpResult(
    HttpStatusCode FormStatus,
    HttpStatusCode DraftStatus,
    HttpStatusCode FormUpdateStatus,
    HttpStatusCode UpdateStatus,
    HttpStatusCode PublishDraftUpdateStatus,
    HttpStatusCode PublishStatus,
    HttpStatusCode StartStatus,
    HttpStatusCode ArchiveStatus,
    string? UpdatedPageTitle,
    string? PublishedStatus,
    string? ProcessStatus,
    string? ArchivedStatus);
