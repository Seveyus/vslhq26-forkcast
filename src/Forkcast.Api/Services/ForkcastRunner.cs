using Forkcast.Core.Ai;
using Forkcast.Core.Decisions;
using Forkcast.Core.Demo;
using Forkcast.Core.Incidents;
using Forkcast.Core.Simulation;

namespace Forkcast.Api.Services;

/// <summary>An incident ready to run, plus everything worth telling the user about how it was read.</summary>
public sealed record ResolvedIncident(
    Incident Incident,
    string Source,
    IReadOnlyList<string> Notes,
    IReadOnlyList<DraftAdjustment> Adjustments);

/// <summary>
/// The application service behind the endpoints: read the incident, then decide.
/// </summary>
public sealed class ForkcastRunner(
    IIncidentIntelligence intelligence,
    IncidentComposer composer,
    DecisionService decisions)
{
    /// <summary>
    /// Turns incident text into a runnable incident. Empty text means the preloaded demo, which
    /// keeps the published seed reproducing the published numbers.
    /// </summary>
    public async Task<ResolvedIncident> ResolveAsync(
        string? narrative,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(narrative))
        {
            return new ResolvedIncident(DemoScenario.Incident, "demo", [], []);
        }

        ExtractionResult extraction;
        try
        {
            extraction = await intelligence.ExtractAsync(narrative, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The reading step is an enhancement, not a dependency. Falling back to the site
            // template is better than refusing to answer.
            return new ResolvedIncident(
                DemoScenario.Incident,
                "fallback",
                ["The incident could not be read automatically, so the site template was used."],
                []);
        }

        var (incident, adjustments) = composer.Compose(
            extraction.Draft, DemoScenario.Incident, narrative);

        return new ResolvedIncident(incident, extraction.Source, extraction.Notes, adjustments);
    }

    public async Task<(DecisionResult Result, ResolvedIncident Resolved)> RunAsync(
        string? narrative,
        SimulationOptions options,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(narrative, cancellationToken);
        var plans = DemoScenario.PlansFor(resolved.Incident);
        var result = await decisions.DecideAsync(resolved.Incident, plans, options, cancellationToken);

        return (result, resolved);
    }

    public async Task<(DecisionResult Result, ResolvedIncident Resolved)> ChallengeAsync(
        string? narrative,
        string question,
        SimulationOptions options,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(narrative, cancellationToken);
        var plans = DemoScenario.PlansFor(resolved.Incident);
        var result = await decisions.ChallengeAsync(
            resolved.Incident, plans, question, options, cancellationToken);

        return (result, resolved);
    }
}
