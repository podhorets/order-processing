using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Options;
using OrderService.Infrastructure.Messaging.Outbox;

namespace OrderService.Infrastructure.Messaging;

public static class BackgroundJobExtensions
{
    public static void AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutBoxJobSettings>(configuration.GetSection("BackgroundJobs:Outbox"));

        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(configuration.GetConnectionString("Database"))));

        services.AddHangfireServer();

        services.AddScoped<IProcessOutboxMessagesJob, ProcessOutboxMessagesJob>();
    }

    public static IApplicationBuilder UseBackgroundJobs(this WebApplication app)
    {
        app.Services
            .GetRequiredService<IRecurringJobManager>()
            .AddOrUpdate<IProcessOutboxMessagesJob>(
                "outbox-processor",
                job => job.ProcessAsync(CancellationToken.None),
                app.Services.GetRequiredService<IOptions<OutBoxJobSettings>>().Value.Schedule);

        return app;
    }
}
