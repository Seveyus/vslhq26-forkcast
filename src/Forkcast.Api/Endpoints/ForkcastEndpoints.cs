using Forkcast.Api.Contracts;
using Forkcast.Api.Services;
using Forkcast.Core.Ai;
using Forkcast.Core.Demo;
using Forkcast.Core.Incidents;
using Forkcast.Core.Simulation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Forkcast.Api.Endpoints;

public static class ForkcastEndpoints
{
    private const int MaxNarrativeLength = 4000;

    private const int MaxQuestionLength = 500;

    private const int MaxTrialCount = 2000;

    public const string SuggestedChallenge =
        "What happens if the temporary battery arrives one hour late?";

    private static readonly string[] ExampleChallenges =
    [
        SuggestedChallenge,
        "What if the battery unit cannot be sourced at all?",
        "What if another charge point goes down?",
        "What if the fast charger is repaired overnight?",
        "What if every route has to depart 45 minutes earlier?"
    ];

    public static IEndpointRouteBuilder MapForkcast(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Forkcast");

        api.MapGet("/health", (IIncidentIntelligence intelligence) => TypedResults.Ok(
                new HealthResponse(
                    "healthy",
                    "Forkcast",
                    ForkcastVersion.Current,
                    intelligence.ProviderName,
                    intelligence.IsLive,
                    SimulationOptions.DefaultSeed,
                    SimulationOptions.DefaultTrialCount)))
            .WithName("GetHealth")
            .WithSummary("Liveness, version and which intelligence provider is answering.");

        api.MapGet("/demo/incident", () => TypedResults.Ok(
                new DemoIncidentResponse(
                    DemoScenario.Incident.ToDto(),
                    DemoScenario.NarrativeText,
                    DemoScenario.Plans.Select(p => p.ToDto()).ToList(),
                    SuggestedChallenge,
                    ExampleChallenges,
                    SimulationOptions.DefaultSeed,
                    SimulationOptions.DefaultTrialCount)))
            .WithName("GetDemoIncident")
            .WithSummary("The preloaded demonstration incident and the two response plans.");

        api.MapGet("/demo/result", async (
                ForkcastRunner runner,
                CancellationToken cancellationToken) =>
            {
                var (result, _) = await runner.RunAsync(
                    null, SimulationOptions.Default, cancellationToken);
                return TypedResults.Ok(result.ToResponse());
            })
            .WithName("GetDemoResult")
            .WithSummary("The full decision for the demonstration incident at the published seed.");

        api.MapPost("/incidents/parse", async Task<Results<Ok<ParseIncidentResponse>, ProblemHttpResult>> (
                ParseIncidentRequest request,
                ForkcastRunner runner,
                CancellationToken cancellationToken) =>
            {
                if (Validate.Narrative(request.Narrative, required: true) is { } problem)
                {
                    return problem;
                }

                var resolved = await runner.ResolveAsync(request.Narrative, cancellationToken);

                return TypedResults.Ok(new ParseIncidentResponse(
                    resolved.Incident.ToDto(),
                    resolved.Source,
                    resolved.Notes,
                    resolved.Adjustments.Select(a => a.ToDto()).ToList()));
            })
            .WithName("ParseIncident")
            .WithSummary("Reads incident text into the structured incident the engine will run.");

        api.MapPost("/simulations/run", async Task<Results<Ok<DecisionResponse>, ProblemHttpResult>> (
                RunSimulationRequest request,
                ForkcastRunner runner,
                CancellationToken cancellationToken) =>
            {
                if (Validate.Narrative(request.Narrative, required: false) is { } narrativeProblem)
                {
                    return narrativeProblem;
                }

                if (!Validate.TryBuildOptions(request.Seed, request.TrialCount, out var options, out var problem))
                {
                    return problem!;
                }

                var (result, resolved) = await runner.RunAsync(
                    request.Narrative, options, cancellationToken);

                return TypedResults.Ok(result.ToResponse(BuildNotes(resolved)));
            })
            .WithName("RunSimulation")
            .WithSummary("Simulates both response plans and returns the verified recommendation.");

        api.MapPost("/simulations/challenge", async Task<Results<Ok<DecisionResponse>, ProblemHttpResult>> (
                ChallengeRequest request,
                ForkcastRunner runner,
                CancellationToken cancellationToken) =>
            {
                if (Validate.Narrative(request.Narrative, required: false) is { } narrativeProblem)
                {
                    return narrativeProblem;
                }

                if (Validate.Question(request.Question) is { } questionProblem)
                {
                    return questionProblem;
                }

                if (!Validate.TryBuildOptions(request.Seed, request.TrialCount, out var options, out var problem))
                {
                    return problem!;
                }

                var (result, resolved) = await runner.ChallengeAsync(
                    request.Narrative, request.Question, options, cancellationToken);

                return TypedResults.Ok(result.ToResponse(BuildNotes(resolved)));
            })
            .WithName("ChallengeSimulation")
            .WithSummary("Reruns the simulation with one assumption changed and reports the difference.");

        return app;
    }

    private static List<string> BuildNotes(ResolvedIncident resolved) =>
        [.. resolved.Notes, .. resolved.Adjustments.Select(a => a.Reason)];

    private static class Validate
    {
        public static ProblemHttpResult? Narrative(string? narrative, bool required)
        {
            if (string.IsNullOrWhiteSpace(narrative))
            {
                return required
                    ? Problem("A narrative is required.", "Describe the incident in plain language.")
                    : null;
            }

            return narrative.Length > MaxNarrativeLength
                ? Problem(
                    "The narrative is too long.",
                    $"Incident text must be {MaxNarrativeLength} characters or fewer.")
                : null;
        }

        public static ProblemHttpResult? Question(string? question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return Problem(
                    "A question is required.",
                    $"Ask what to change, for example: \"{SuggestedChallenge}\"");
            }

            return question.Length > MaxQuestionLength
                ? Problem(
                    "The question is too long.",
                    $"Questions must be {MaxQuestionLength} characters or fewer.")
                : null;
        }

        public static bool TryBuildOptions(
            long? seed,
            int? trialCount,
            out SimulationOptions options,
            out ProblemHttpResult? problem)
        {
            options = SimulationOptions.Default;
            problem = null;

            if (trialCount is { } count && count is < 1 or > MaxTrialCount)
            {
                problem = Problem(
                    "Unusable trial count.",
                    $"Trial count must be between 1 and {MaxTrialCount}.");
                return false;
            }

            options = new SimulationOptions
            {
                Seed = seed ?? SimulationOptions.DefaultSeed,
                TrialCount = trialCount ?? SimulationOptions.DefaultTrialCount
            };

            return true;
        }

        private static ProblemHttpResult Problem(string title, string detail) =>
            TypedResults.Problem(new ProblemDetails
            {
                Title = title,
                Detail = detail,
                Status = StatusCodes.Status400BadRequest,
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1"
            });
    }
}

/// <summary>Single place the product version is stated.</summary>
public static class ForkcastVersion
{
    public const string Current = "1.0.0";
}
