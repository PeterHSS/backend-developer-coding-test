using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

namespace Api.Extensions;

public static class RedisExtension
{
    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")!;

        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(connectionString));

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
        });

        services.AddSingleton<IDistributedLockFactory>(sp =>
        {
            var connection = sp.GetRequiredService<IConnectionMultiplexer>();

            return RedLockFactory.Create(new List<RedLockMultiplexer> { new(connection) });
        });

        return services;
    }
}
