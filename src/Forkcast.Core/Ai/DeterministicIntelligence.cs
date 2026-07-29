using Forkcast.Core.Challenges;
using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;

namespace Forkcast.Core.Ai;

/// <summary>
/// The no-credentials implementation. Reads incidents with pattern matching and declines to
/// write prose, so the deterministic summary is used instead.
/// </summary>
/// <remarks>
/// Forkcast is designed so that this implementation is enough to run the entire product. Azure
/// OpenAI improves the reading of unusual phrasing and the quality of the written explanation;
/// it is never load-bearing for a decision.
/// </remarks>
public sealed class DeterministicIntelligence(ChallengeService challenges) : IIncidentIntelligence
{
    private readonly ChallengeService _challenges =
        challenges ?? throw new ArgumentNullException(nameof(challenges));

    public string ProviderName => "Deterministic";

    public bool IsLive => false;

    public Task<ExtractionResult> ExtractAsync(
        string narrative,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(narrative);

        var times = TextFacts.ClockTimes(narrative);
        var range = TextFacts.PercentRange(narrative);
        var notes = new List<string>();

        var draft = new IncidentDraft
        {
            DetectedAtLocalTime = times.Count > 0 ? times[0] : null,
            DeadlineLocalTime = times.Count > 1 ? times[^1] : null,
            VehicleCount = TextFacts.CountBefore(narrative, "vehicles", "vans", "trucks"),
            OperationalChargePointCount = TextFacts.CountBefore(
                narrative, "chargers", "charge points", "charging points", "connectors"),
            PriorityVehicleCount = TextFacts.CountBefore(narrative, "priority routes", "priority"),
            MinInitialStateOfChargePct = range?.Min,
            MaxInitialStateOfChargePct = range?.Max,
            Failures = []
        };

        if (draft.VehicleCount is null)
        {
            notes.Add("No vehicle count found in the text; the site roster was used.");
        }

        if (draft.OperationalChargePointCount is null)
        {
            notes.Add("No charge point count found in the text; the site inventory was used.");
        }

        notes.Add("Read without a language model. Connect Azure OpenAI for free-form incident text.");

        return Task.FromResult(new ExtractionResult
        {
            Draft = draft,
            Source = "deterministic",
            Notes = notes
        });
    }

    public Task<IReadOnlyList<PlanNarrative>> DescribePlansAsync(
        Incident incident,
        IReadOnlyList<ResponsePlan> plans,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plans);

        IReadOnlyList<PlanNarrative> narratives = plans
            .Select(p => new PlanNarrative { PlanId = p.Id, Name = p.Name, Description = p.Description })
            .ToList();

        return Task.FromResult(narratives);
    }

    /// <summary>
    /// Returns null on purpose. Without a language model there is nothing to add beyond the
    /// deterministic summary, and inventing wording here would blur the boundary the product
    /// exists to keep sharp.
    /// </summary>
    public Task<string?> WriteExecutiveSummaryAsync(
        ExecutiveSummaryRequest request,
        CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public Task<AssumptionOverride> InterpretChallengeAsync(
        string question,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_challenges.Interpret(question));
}
