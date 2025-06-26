using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PrevencaoIncendio.Caching;
using PrevencaoIncendio.Config.Danger;
using PrevencaoIncendio.Config.Ip;
using PrevencaoIncendio.Data;
using PrevencaoIncendio.Mqtt;
using PrevencaoIncendio.Repositories;
using StackExchange.Redis;
using System.Text.Json;

namespace PrevencaoIncendio.Config;

public static class ConfigureAplication
{
    public static void ConfigureApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ConfigureMongoDB(services, configuration);
        ConfigureRedis(services);
        ConfigureDangerParameters(services, configuration);

        services.AddTransient<IValoresRepository, ValoresRepository>();
        services.Configure<IpAddress>(configuration.GetSection("IpAddress"));

        var isMqttOnline = configuration.GetValue<bool>("ConnectionStrings:IsMqttOnline");
        if (isMqttOnline)
        {
            services.AddHostedService<MqttWorker>();
        }
    }
    private static void ConfigureRedis(IServiceCollection services)
    {
        var path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "connectionStringRedis.env"));
        var connectionStringPrd = File.ReadAllText(path);

        var configFromFile = JsonSerializer.Deserialize<CacheConnectionString>(connectionStringPrd)
            ?? throw new InvalidOperationException("Falha ao desserializar CacheConfig");

        services.Configure<CacheConnectionString>(options =>
        {
            options.EndPoints = configFromFile.EndPoints;
            options.Password = configFromFile.Password;
            options.Ssl = configFromFile.Ssl;
            options.SslHost = configFromFile.SslHost;
            options.User = configFromFile.User;
            options.AbortOnConnectFail = configFromFile.AbortOnConnectFail;

        });

        services.AddTransient(sp =>
        {
            var cacheConfig = sp.GetRequiredService<IOptions<CacheConnectionString>>().Value;

            var config = new ConfigurationOptions
            {
                EndPoints = { cacheConfig.EndPoints },
                Password = cacheConfig.Password,
                Ssl = cacheConfig.Ssl,
                SslHost = cacheConfig.SslHost,
                User = cacheConfig.User,
                AbortOnConnectFail = cacheConfig.AbortOnConnectFail
            };
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(config);

            redis = ConnectionMultiplexer.Connect(config);
            return redis.GetDatabase();
        });
    }
    private static void ConfigureMongoDB(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IMongoClient>(sp =>
        {
            var isProduction = configuration.GetValue<bool>("ConnectionStrings:IsProduction");
            var connectionStringDev = configuration.GetConnectionString("Default");

            var path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "connectionString.env"));
            var connectionStringPrd = File.ReadAllText(path);

            var settings = MongoClientSettings.FromConnectionString(isProduction ? connectionStringPrd : connectionStringDev);
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);
            return new MongoClient(settings);
        });

        services.AddTransient(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            return client.GetDatabase(DbManager.DatabaseName);
        });
    }

    private static void ConfigureDangerParameters(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient(options =>
        {
            var dangerParameters = configuration.GetSection("DangerParameters").Get<DangerParameters>();
            if (dangerParameters is null)
            {
                throw new InvalidOperationException("DangerParameters não configurado corretamente.");
            }

            return dangerParameters;
        });
    }
}
