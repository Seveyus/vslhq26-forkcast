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

    /// <summary>
    /// Paragraphs written to be submitted to the verifier, so the guarantee can be tested rather
    /// than taken on trust. Each one reads plausibly; only the last is actually supportable.
    /// </summary>
    private static readonly ProbeExampleDto[] ExampleProbes =
    [
        new(
            "One invented figure",
            "Reprioritising the yard queue and calling out the battery unit lifts on-time "
            + "departures to 97.2%, and avoids roughly £4,200 of contractual penalty exposure "
            + "across the six priority routes.",
            "The 97.2% is a claim. The £4,200 is not — no simulation output produces it."),
        new(
            "Confident and entirely invented",
            "This response recovers 14 hours of depot throughput, improves fleet utilisation by "
            + "23%, and pays for itself within 5 weeks.",
            "Three figures, none of them traceable to anything the engine computed."),
        new(
            "A plausible rounding",
            "On-time departures reach 98% with zero vehicles left at risk.",
            "Close to the truth is still not the truth: the run returns 97.2% and one vehicle at "
            + "risk, so neither figure is supported."),
        new(
            "Honest, and it passes",
            "Reprioritising the queue and activating the battery buffer raises expected on-time "
            + "departures from 60.9% to 97.2%, a gain of 36.3 pp, and cuts vehicles at risk from "
            + "9 to 1 across the 20 in the fleet.",
            "Every figure here is a claim value or an incident fact, so it survives the check.")
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
                    ExampleProbes,
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

        api.MapPost("/verification/probe", async Task<Results<Ok<VerificationProbeResponse>, ProblemHttpResult>> (
                VerificationProbeRequest request,
                ForkcastRunner runner,
                CancellationToken cancellationToken) =>
            {
                if (Validate.Narrative(request.Narrative, required: false) is { } narrativeProblem)
                {
                    return narrativeProblem;
                }

                if (Validate.Submitted(request.Submitted) is { } submittedProblem)
                {
                    return submittedProblem;
                }

                if (!Validate.TryBuildOptions(request.Seed, request.TrialCount, out var options, out var problem))
                {
                    return problem!;
                }

                var probe = await runner.ProbeAsync(
                    request.Narrative, request.Submitted, options, cancellationToken);

                return TypedResults.Ok(probe.ToResponse());
            })
            .WithName("ProbeVerification")
            .WithSummary(
                "Submits an arbitrary paragraph to the claim verifier and reports a verdict on "
                + "every number in it. The same verifier and claim set the product applies to its "
                + "own generated prose.");

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

        public static ProblemHttpResult? Submitted(string? submitted)
        {
            if (string.IsNullOrWhiteSpace(submitted))
            {
                return Problem(
                    "Nothing was submitted.",
                    "Paste a paragraph for the verifier to check.");
            }

            return submitted.Length > MaxNarrativeLength
                ? Problem(
                    "The paragraph is too long.",
                    $"Submitted text must be {MaxNarrativeLength} characters or fewer.")
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
