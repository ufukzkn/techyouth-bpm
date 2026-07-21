using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using TechYouthBpm.Application.Auth;

namespace TechYouthBpm.Infrastructure.Services;

public interface ISessionValidationCache
{
    bool TryGet(string tokenHash, out UserDto? user);
    void Set(string tokenHash, Guid userId, Guid? communityId, DateTime expiresAt, UserDto user);
    void InvalidateTokenHash(string tokenHash);
    void InvalidateUser(Guid userId);
    void InvalidateCommunity(Guid communityId);
}

internal sealed class SessionValidationCache(
    IMemoryCache memoryCache,
    IConfiguration configuration) : ISessionValidationCache
{
    private const int DefaultDurationSeconds = 15;
    private readonly ConcurrentDictionary<string, CacheIndex> entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> userTokens = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> communityTokens = new();

    public bool TryGet(string tokenHash, out UserDto? user)
    {
        if (memoryCache.TryGetValue(CacheKey(tokenHash), out UserDto? cachedUser) && cachedUser is not null)
        {
            user = cachedUser;
            return true;
        }

        RemoveIndexes(tokenHash);
        user = null;
        return false;
    }

    public void Set(string tokenHash, Guid userId, Guid? communityId, DateTime expiresAt, UserDto user)
    {
        var configuredSeconds = int.TryParse(configuration["Auth:SessionCacheSeconds"], out var parsedSeconds)
            ? parsedSeconds
            : DefaultDurationSeconds;
        if (configuredSeconds <= 0)
        {
            return;
        }
        var cacheDuration = TimeSpan.FromSeconds(Math.Clamp(configuredSeconds, 1, 60));
        var remainingSessionDuration = expiresAt - DateTime.UtcNow;
        if (remainingSessionDuration <= TimeSpan.Zero)
        {
            return;
        }

        RemoveIndexes(tokenHash);
        entries[tokenHash] = new CacheIndex(userId, communityId);
        AddIndex(userTokens, userId, tokenHash);
        if (communityId is { } scopedCommunityId)
        {
            AddIndex(communityTokens, scopedCommunityId, tokenHash);
        }

        memoryCache.Set(
            CacheKey(tokenHash),
            user,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheDuration < remainingSessionDuration
                    ? cacheDuration
                    : remainingSessionDuration,
                Size = 1
            }.RegisterPostEvictionCallback((_, _, _, state) =>
            {
                var (cache, hash) = ((SessionValidationCache Cache, string Hash))state!;
                cache.RemoveIndexes(hash);
            }, (this, tokenHash)));
    }

    public void InvalidateTokenHash(string tokenHash)
    {
        memoryCache.Remove(CacheKey(tokenHash));
        RemoveIndexes(tokenHash);
    }

    public void InvalidateUser(Guid userId) => InvalidateIndex(userTokens, userId);

    public void InvalidateCommunity(Guid communityId) => InvalidateIndex(communityTokens, communityId);

    private void InvalidateIndex(
        ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> index,
        Guid id)
    {
        if (!index.TryRemove(id, out var tokenHashes))
        {
            return;
        }

        foreach (var tokenHash in tokenHashes.Keys)
        {
            memoryCache.Remove(CacheKey(tokenHash));
            RemoveIndexes(tokenHash);
        }
    }

    private void RemoveIndexes(string tokenHash)
    {
        if (!entries.TryRemove(tokenHash, out var index))
        {
            return;
        }

        RemoveIndexValue(userTokens, index.UserId, tokenHash);
        if (index.CommunityId is { } communityId)
        {
            RemoveIndexValue(communityTokens, communityId, tokenHash);
        }
    }

    private static void AddIndex(
        ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> index,
        Guid id,
        string tokenHash) =>
        index.GetOrAdd(id, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[tokenHash] = 0;

    private static void RemoveIndexValue(
        ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> index,
        Guid id,
        string tokenHash)
    {
        if (!index.TryGetValue(id, out var tokenHashes))
        {
            return;
        }

        tokenHashes.TryRemove(tokenHash, out _);
        if (tokenHashes.IsEmpty)
        {
            index.TryRemove(new KeyValuePair<Guid, ConcurrentDictionary<string, byte>>(id, tokenHashes));
        }
    }

    private static string CacheKey(string tokenHash) => $"auth-session:{tokenHash}";

    private sealed record CacheIndex(Guid UserId, Guid? CommunityId);
}

internal sealed class NullSessionValidationCache : ISessionValidationCache
{
    public static readonly NullSessionValidationCache Instance = new();

    private NullSessionValidationCache()
    {
    }

    public bool TryGet(string tokenHash, out UserDto? user)
    {
        user = null;
        return false;
    }

    public void Set(string tokenHash, Guid userId, Guid? communityId, DateTime expiresAt, UserDto user)
    {
    }

    public void InvalidateTokenHash(string tokenHash)
    {
    }

    public void InvalidateUser(Guid userId)
    {
    }

    public void InvalidateCommunity(Guid communityId)
    {
    }
}
