using System.Text.Json.Serialization;
using Forkcast.Api.Ai;
using Forkcast.Api.Configuration;
using Forkcast.Api.Diagnostics;
using Forkcast.Api.Endpoints;
using Forkcast.Api.Services;
using Forkcast.Core.Challenges;
using Forkcast.Core.Comparison;
using Forkcast.Core.Decisions;
using Forkcast.Core.Incidents;
using Forkcast.Core.Recommendations;
using Forkcast.Core.Simulation;
using Forkcast.Core.Verification;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

const string LocalFrontendCors = "forkcast-local";

// Makes the documented flow work: copy .env.example to .env and the API picks it up. A real
// shell export still wins, and .env is git-ignored.
if (DotEnv.Locate(builder.Environment.ContentRootPath) is { } envFile)
{
    builder.Configuration.AddDotEnvFile(envFile);
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Named origins only, never AllowAnyOrigin. The policy is applied unconditionally rather than
// only in Development so that `dotnet run` serves the local frontend whatever environment the
// host happens to resolve to; the allow-list is what makes that safe, not the environment check.
builder.Services.AddCors(options => options.AddPolicy(LocalFrontendCors, policy => policy
    .WithOrigins(
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:4173",
        "http://127.0.0.1:4173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

// The decision pipeline. Every component is small enough to read in one sitting, and the whole
// graph is registered here rather than discovered, so it is visible in one place.
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddSingleton<ComparisonService>();
builder.Services.AddSingleton<ClaimSetBuilder>();
builder.Services.AddSingleton<ClaimVerifier>();
builder.Services.AddSingleton<RecommendationService>();
builder.Services.AddSingleton<ChallengeService>();
builder.Services.AddSingleton<IncidentComposer>();

// Scoped, because it depends on the intelligence provider, which may be a typed HttpClient.
builder.Services.AddScoped<DecisionService>();
builder.Services.AddScoped<ForkcastRunner>();

// The language boundary. Azure OpenAI when credentials are present, the deterministic provider
// otherwise. Nothing else in the graph depends on a model being reachable.
builder.Services.AddForkcastIntelligence(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    Log.UnhandledFailure(app.Logger, feature?.Error, context.Request.Path);

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Title = "Forkcast could not complete this request.",
        Detail = "The simulation did not finish. Retry, or reload the demonstration incident.",
        Status = StatusCodes.Status500InternalServerError
    });
}));

app.UseStatusCodePages();
app.UseCors(LocalFrontendCors);

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Forkcast API"));

app.MapForkcast();
app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

app.LogIntelligenceProvider();
app.Run();

/// <summary>Exposed so the test project can start the API in memory.</summary>
public partial class Program;
