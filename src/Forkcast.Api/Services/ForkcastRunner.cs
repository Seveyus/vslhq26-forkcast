using Forkcast.Api.Ai;
using Forkcast.Core.Ai;
using Forkcast.Core.Briefing;
using Forkcast.Core.Decisions;
using Forkcast.Core.Demo;
using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;
using Forkcast.Core.Simulation;
using Forkcast.Core.Verification;

namespace Forkcast.Api.Services;

/// <summary>An incident ready to run, plus everything worth telling the user about how it was read.</summary>
public sealed record ResolvedIncident(
    Incident Incident,
    string Source,
    IReadOnlyList<string> Notes,
    IReadOnlyList<DraftAdjustment> Adjustments);

/// <summary>The verifier's verdict on a paragraph somebody submitted to it.</summary>
public sealed record VerificationProbe(
    bool Accepted,
    string Submitted,
    IReadOnlyList<NumberFinding> Findings,
    string Displayed,
    string DisplayedSource,
    IReadOnlyList<Claim> Claims,
    long Seed,
    int TrialCount);

/// <summary>
/// The application service behind the endpoints: read the incident, then decide.
/// </summary>
public sealed class ForkcastRunner(
    IIncidentIntelligence intelligence,
    IncidentComposer composer,
    DecisionService decisions,
    ClaimVerifier verifier,
    BriefingComposer briefings)
{
    /// <summary>
    /// Turns incident text into a runnable incident. Empty text means the preloaded demo, which
    /// keeps the published seed reproducing the published numbers.
    /// </summary>
    public async Task<ResolvedIncident> ResolveAsync(
        string? narrative,
        string? scenarioKey = null,
        CancellationToken cancellationToken = default)
    {
        var scenario = ScenarioCatalog.Resolve(scenarioKey);

        if (string.IsNullOrWhiteSpace(narrative) || narrative.Trim() == scenario.Narrative.Trim())
        {
            return new ResolvedIncident(scenario.Incident, "demo", [], []);
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
                scenario.Incident,
                "fallback",
                ["The incident could not be read automatically, so the site template was used."],
                []);
        }

        var (incident, adjustments) = composer.Compose(
            extraction.Draft, scenario.Incident, narrative);

        return new ResolvedIncident(incident, extraction.Source, extraction.Notes, adjustments);
    }

    public async Task<(DecisionResult Result, ResolvedIncident Resolved)> RunAsync(
        string? narrative,
        SimulationOptions options,
        string? scenarioKey = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(narrative, scenarioKey, cancellationToken);
        var plans = await WordPlansAsync(resolved.Incident, scenarioKey, cancellationToken);
        var result = await decisions.DecideAsync(resolved.Incident, plans, options, cancellationToken);

        return (result, resolved);
    }

    public async Task<(DecisionResult Result, ResolvedIncident Resolved)> ChallengeAsync(
        string? narrative,
        string question,
        SimulationOptions options,
        string? scenarioKey = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(narrative, scenarioKey, cancellationToken);
        var plans = await WordPlansAsync(resolved.Incident, scenarioKey, cancellationToken);
        var result = await decisions.ChallengeAsync(
            resolved.Incident, plans, question, options, cancellationToken);

        return (result, resolved);
    }

    /// <summary>
    /// Runs an arbitrary paragraph past the verifier, against the real claim set for the
    /// incident, and reports a verdict on every number in it.
    /// </summary>
    /// <remarks>
    /// This is the guarantee offered for inspection rather than asserted. Anyone can hand it a
    /// convincing-sounding paragraph and watch which figures it can account for and which it
    /// cannot. It is the same <see cref="ClaimVerifier"/> instance and the same claim set the
    /// product uses on its own generated prose — there is no separate, friendlier check here.
    /// </remarks>
    public async Task<VerificationProbe> ProbeAsync(
        string? narrative,
        string submitted,
        SimulationOptions options,
        string? scenarioKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(submitted);

        var (result, _) = await RunAsync(narrative, options, scenarioKey, cancellationToken);
        var claims = result.Verification.Claims;
        var context = VerificationContext.FromIncident(result.Incident, options);

        var findings = verifier.AnalyseNumbers(submitted, claims, context);
        var accepted = findings.TrueForAll(finding => finding.Supported);

        return new VerificationProbe(
            Accepted: accepted,
            Submitted: submitted.Trim(),
            Findings: findings,
            Displayed: accepted ? submitted.Trim() : result.Recommendation.DeterministicSummary,
            DisplayedSource: accepted ? "submitted" : "deterministic",
            Claims: claims,
            Seed: options.Seed,
            TrialCount: options.TrialCount);
    }

    /// <summary>
    /// Composes a decision brief from the current verified state, after applying a counterfactual
    /// if one is supplied.
    /// </summary>
    /// <remarks>
    /// The brief is a projection, not a template: switch domain and its beats re-word themselves,
    /// change an assumption and the counterfactual beat carries the real before-and-after. Because
    /// every caption is built from claim display values, the payload cannot introduce a figure the
    /// claim set does not already carry.
    /// </remarks>
    public async Task<(DecisionBriefing Briefing, DecisionResult Result)> BriefAsync(
        string? narrative,
        string? question,
        SimulationOptions options,
        string? scenarioKey = null,
        CancellationToken cancellationToken = default)
    {
        var (result, _) = string.IsNullOrWhiteSpace(question)
            ? await RunAsync(narrative, options, scenarioKey, cancellationToken)
            : await ChallengeAsync(narrative, question, options, scenarioKey, cancellationToken);

        return (briefings.Compose(result), result);
    }

    /// <summary>
    /// Lets the model phrase the two plans, and nothing more.
    /// </summary>
    /// <remarks>
    /// Only the description text is taken. The charging policy, the charge target, the buffer and
    /// its costs are all left exactly as the domain defines them, so no wording the model produces
    /// can change what is simulated. Wording that fails <see cref="PlanWording"/> is dropped.
    /// </remarks>
    private async Task<IReadOnlyList<ResponsePlan>> WordPlansAsync(
        Incident incident,
        string? scenarioKey,
        CancellationToken cancellationToken)
    {
        var plans = ScenarioCatalog.Resolve(scenarioKey).PlansFor(incident);

        if (!intelligence.IsLive)
        {
            return plans;
        }

        try
        {
            var narratives = await intelligence.DescribePlansAsync(incident, plans, cancellationToken);
            var byId = narratives.ToDictionary(n => n.PlanId, StringComparer.Ordinal);

            return plans
                .Select(plan =>
                    byId.TryGetValue(plan.Id, out var wording)
                    && PlanWording.IsAcceptable(wording.Description)
                        ? plan with { Description = wording.Description }
                        : plan)
                .ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return plans;
        }
    }
}
