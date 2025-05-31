using StackExchange.Redis;
using System.Text.Json;

namespace PrevencaoIncendio.Extensions;

public static class RedisDatabaseExtensions
{
    public static async Task SetRecordAsync<T>(
    this IDatabase cache,
    string recordId,
    T data,
    TimeSpan? absoluteExpireTime = null)
    {
        var expirationStr = AppSettingsProvider.Configuration["Redis:DefaultExpiration"];

        TimeSpan defaultExpiration = TimeSpan.TryParse(expirationStr, out var parsed)
            ? parsed
            : TimeSpan.FromMinutes(1);

        var expiration = absoluteExpireTime ?? defaultExpiration;

        var jsonData = JsonSerializer.Serialize(data);

        await cache.StringSetAsync(recordId, jsonData, expiration);
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
