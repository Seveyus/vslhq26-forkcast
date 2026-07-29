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

    /// <summary>Shown in validation messages as the shape of a usable question.</summary>
    public static string SuggestedChallenge => ScenarioCatalog.Fleet.SuggestedChallenge;

    /// <summary>
    /// Paragraphs written to be submitted to the verifier, so the guarantee can be tested rather
    /// than taken on trust.
    /// </summary>
    /// <remarks>
    /// Composed from the scenario's own vocabulary, so the demonstration reads correctly in any
    /// domain. The three attacks carry figures no simulation can produce, which is what makes them
    /// attacks. The fourth uses only facts stated in the incident — counts the allow-list can
    /// account for — so it passes by construction and cannot drift out of date.
    /// </remarks>
    private static IReadOnlyList<ProbeExampleDto> ProbesFor(string scenarioKey)
    {
        var scenario = ScenarioCatalog.Resolve(scenarioKey);
        var words = scenario.Incident.Vocabulary;
        var units = scenario.Incident.VehicleCount;
        var resources = scenario.Incident.OperationalChargePointCount;
        var priority = scenario.Incident.PriorityVehicleCount;

        return
        [
            new(
                "One invented figure",
                $"Acting on this lifts {words.OnTimeMetricLabel} sharply, and avoids roughly "
                + $"£4,200 of contractual penalty exposure across the {priority} "
                + $"{words.PriorityLabelPlural}.",
                $"The {priority} is an incident fact. The £4,200 is not \u2014 no simulation output "
                + "produces it."),
            new(
                "Confident and entirely invented",
                $"This response recovers 14 hours of {words.DomainLabel.ToLowerInvariant()} "
                + "throughput, improves utilisation by 23%, and pays for itself within 5 weeks.",
                "Three figures, none of them traceable to anything the engine computed."),
            new(
                "A plausible rounding",
                $"{Capitalise(words.OnTimeMetricLabel)} reach 98% with zero {words.UnitPlural} "
                + "left at risk.",
                "Close to the truth is still not the truth. Neither figure is a claim value, so "
                + "neither is supported."),
            new(
                "Honest, and it passes",
                $"All {units} {words.UnitPlural} are competing for {resources} "
                + $"{words.Resources(resources)}, and {priority} of them are "
                + $"{words.PriorityLabelPlural}.",
                "Every figure here is a fact stated in the incident, so it survives the check.")
        ];
    }

    private static string Capitalise(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

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

        api.MapGet("/scenarios", () => TypedResults.Ok(
                ScenarioCatalog.All.Select(s => s.ToSummaryDto()).ToList()))
            .WithName("GetScenarios")
            .WithSummary(
                "The shipped incidents. Two unrelated domains run on the same engine; adding one "
                + "is a matter of supplying data, not of editing the decision logic.");

        api.MapGet("/demo/incident", (string? scenario) =>
            {
                var chosen = ScenarioCatalog.Resolve(scenario);
                return TypedResults.Ok(new DemoIncidentResponse(
                    chosen.Incident.ToDto(),
                    chosen.Narrative,
                    chosen.Plans.Select(p => p.ToDto()).ToList(),
                    chosen.SuggestedChallenge,
                    chosen.ExampleChallenges,
                    ProbesFor(chosen.Key),
                    ScenarioCatalog.All.Select(s => s.ToSummaryDto()).ToList(),
                    chosen.Key,
                    SimulationOptions.DefaultSeed,
                    SimulationOptions.DefaultTrialCount));
            })
            .WithName("GetDemoIncident")
            .WithSummary("A preloaded incident and its two response plans.");

        api.MapGet("/demo/result", async (
                string? scenario,
                ForkcastRunner runner,
                CancellationToken cancellationToken) =>
            {
                var (result, _) = await runner.RunAsync(
                    null, SimulationOptions.Default, scenario, cancellationToken);
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

                var resolved = await runner.ResolveAsync(
                    request.Narrative, request.Scenario, cancellationToken);

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
                    request.Narrative, options, request.Scenario, cancellationToken);

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
                    request.Narrative, request.Question, options, request.Scenario, cancellationToken);

                return TypedResults.Ok(result.ToResponse(BuildNotes(resolved)));
            })
            .WithName("ChallengeSimulation")
            .WithSummary("Reruns the simulation with one assumption changed and reports the difference.");

        api.MapGet("/briefing/export", async Task<Results<Ok<BriefingResponse>, ProblemHttpResult>> (
                string? scenario,
                string? question,
                ForkcastRunner runner,
                CancellationToken cancellationToken) =>
            {
                if (question is not null && Validate.Question(question) is { } problem)
                {
                    return problem;
                }

                var (briefing, result) = await runner.BriefAsync(
                    null, question, SimulationOptions.Default, scenario, cancellationToken);

                return TypedResults.Ok(briefing.ToResponse(result.Incident.Vocabulary));
            })
            .WithName("ExportBriefing")
            .WithSummary(
                "The decision brief for the current verified state: timed beats, canvas state and "
                + "the claims each beat may show. Every caption is composed from claim display "
                + "values, so a renderer cannot introduce a figure the claim set does not carry.");

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
                    request.Narrative, request.Submitted, options, request.Scenario, cancellationToken);

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
