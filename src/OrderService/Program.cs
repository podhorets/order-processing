using FluentValidation;
using Hangfire;
using OrderService.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddBackgroundJobs(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHangfireDashboard(options: new DashboardOptions
    {
        Authorization = []
    });
    app.ApplyMigrations();
}

app.MapHealthChecks("/health");
app.MapEndpoints();
app.UseBackgroundJobs();

app.Run();
