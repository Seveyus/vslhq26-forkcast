namespace Forkcast.Core.Ai;

/// <summary>
/// The shape a language model is allowed to produce when reading an incident report.
/// </summary>
/// <remarks>
/// Every field is optional and every field is an <em>input</em> to the simulation, never a
/// result from it. Counts, times and battery ranges are facts stated by the operator that the
/// model is transcribing. Anything the model cannot find is left null and filled from the
/// site template, and the substitution is reported back to the user.
/// </remarks>
public sealed record IncidentDraft
{
    public string? Title { get; init; }

    public string? Site { get; init; }

    /// <summary>Local clock time the incident was detected, "HH:mm".</summary>
    public string? DetectedAtLocalTime { get; init; }

    /// <summary>Local clock time of the last departure, "HH:mm".</summary>
    public string? DeadlineLocalTime { get; init; }

    public int? VehicleCount { get; init; }

    public int? OperationalChargePointCount { get; init; }

    public int? FailedChargePointCount { get; init; }

    public int? PriorityVehicleCount { get; init; }

    public double? MinInitialStateOfChargePct { get; init; }

    public double? MaxInitialStateOfChargePct { get; init; }

    public IReadOnlyList<string> Failures { get; init; } = [];

    public static IncidentDraft Empty { get; } = new();
}

/// <summary>An adjustment the composer had to make to keep a draft physically sensible.</summary>
public sealed record DraftAdjustment
{
    public required string Field { get; init; }

    public required string Reason { get; init; }
}
