using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace PrevencaoIncendio.Extensions;

public static class DistributedCacheExtensions
{
    public static async Task SetRecordAsync<T>(this IDatabase cache,
        string recordId,
        T data,
        TimeSpan? absoluteExpireTime = null,
        TimeSpan? unusedExpireTime = null)
    {
        var options = new DistributedCacheEntryOptions();

        options.AbsoluteExpirationRelativeToNow = absoluteExpireTime ?? TimeSpan.FromSeconds(60);
        options.SlidingExpiration = unusedExpireTime;

        var jsonData = JsonSerializer.Serialize(data);
        await cache.StringSetAsync(recordId, jsonData, absoluteExpireTime);
    }

    public static async Task<T> GetRecordAsync<T>(this IDatabase cache, string recordId)
    {
        var jsonData = await cache.StringGetAsync(recordId);

        if (!jsonData.HasValue)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(jsonData);
    }
}
