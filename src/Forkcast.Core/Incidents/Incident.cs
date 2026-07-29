namespace Forkcast.Core.Incidents;

/// <summary>
/// A structured operational incident. Everything the decision engine is allowed to reason
/// about lives here: the language model may produce this object from free text, but it never
/// produces the numbers that come out of the simulation.
/// </summary>
public sealed record Incident
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    /// <summary>The operator's own words. Kept verbatim for traceability.</summary>
    public required string Narrative { get; init; }

    public required string Site { get; init; }

    /// <summary>
    /// The domain's own nouns. Every user-visible string derived from this incident is composed
    /// from these, so the engine never hard-codes the vocabulary of one industry.
    /// </summary>
    public required IncidentVocabulary Vocabulary { get; init; }

    public required DateTimeOffset DetectedAt { get; init; }

    public required DateTimeOffset DepartureDeadline { get; init; }

    public required IReadOnlyList<Vehicle> Fleet { get; init; }

    public required IReadOnlyList<ChargePoint> ChargePoints { get; init; }

    public required DepotConstraints Constraints { get; init; }

    public required IReadOnlyList<TariffWindow> Tariff { get; init; }

    /// <summary>Short statements describing what went wrong, for the incident card.</summary>
    public required IReadOnlyList<string> Failures { get; init; }

    public int VehicleCount => Fleet.Count;

    public int PriorityVehicleCount => Fleet.Count(v => v.IsPriorityRoute);

    public int OperationalChargePointCount => ChargePoints.Count(c => c.IsOperational);

    public int FailedChargePointCount => ChargePoints.Count(c => !c.IsOperational);

    public double TotalRequiredEnergyKwh => Fleet.Sum(v => v.RequiredEnergyKwh);

    public double PricePerKwhAt(DateTimeOffset at)
    {
        foreach (var window in Tariff)
        {
            if (at >= window.From && at < window.To)
            {
                return window.PricePerKwhGbp;
            }
        }

        return Tariff.Count > 0 ? Tariff[^1].PricePerKwhGbp : 0.0;
    }

    /// <summary>Average price over a charging session, weighted by time spent in each band.</summary>
    public double AveragePricePerKwh(DateTimeOffset from, DateTimeOffset to)
    {
        var totalMinutes = (to - from).TotalMinutes;
        if (totalMinutes <= 0)
        {
            return PricePerKwhAt(from);
        }

        var weighted = 0.0;
        foreach (var window in Tariff)
        {
            var overlapStart = from > window.From ? from : window.From;
            var overlapEnd = to < window.To ? to : window.To;
            var overlap = (overlapEnd - overlapStart).TotalMinutes;
            if (overlap > 0)
            {
                weighted += overlap * window.PricePerKwhGbp;
            }
        }

        return weighted / totalMinutes;
    }
}
