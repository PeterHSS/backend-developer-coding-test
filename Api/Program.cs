using Api.Domain.Abstractions.Infrastructure;
using Api.Extensions;
using Api.Features.Stories.GetBestStories;
using Api.Infrastructure.ExternalServices.HackerNews;
using Api.Middleware;
using Carter;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddCarter();

builder.Services.AddRedis(builder.Configuration);

builder.Services.AddScoped<IGetBestStoriesUseCase, GetBestStoriesUseCase>();

builder.Services.AddScoped<IHackerNewsService, HackerNewsService>();

var hackerNewsOptions = builder.Configuration.GetSection(HackerNewsOptions.SectionName).Get<HackerNewsOptions>()!;

builder.Services.AddSingleton(hackerNewsOptions);

builder.Services.AddHttpClient<HackerNewsHttpClient>(client =>
{
    client.BaseAddress = new Uri(hackerNewsOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(hackerNewsOptions.TimeoutInSeconds);
}).AddStandardResilienceHandler(resilience =>
{
    resilience.Retry.MaxRetryAttempts = hackerNewsOptions.MaxRetryAttempts;
    resilience.Retry.Delay = TimeSpan.FromMilliseconds(hackerNewsOptions.DelayRetryInMilliseconds);
    resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(hackerNewsOptions.TimeoutInSeconds);
    resilience.CircuitBreaker.SamplingDuration = resilience.AttemptTimeout.Timeout * 2 + TimeSpan.FromSeconds(1);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
        options.DefaultOpenAllTags = true;
    });
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.MapCarter();

app.Run();

public partial class Program { }
