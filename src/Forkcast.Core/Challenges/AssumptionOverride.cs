namespace Forkcast.Core.Challenges;

/// <summary>The assumptions a user is allowed to challenge.</summary>
public enum AssumptionKind
{
    /// <summary>Nothing recognised in the question.</summary>
    None,

    /// <summary>The towed battery unit turns up later than planned. Value is minutes.</summary>
    BufferArrivalDelayMinutes,

    /// <summary>The towed battery unit cannot be sourced at all.</summary>
    BufferUnavailable,

    /// <summary>Further charge points go offline. Value is the count.</summary>
    AdditionalChargePointOutage,

    /// <summary>Departures are brought forward. Value is minutes.</summary>
    DeadlineEarlierMinutes,

    /// <summary>The failed fast charger is repaired during the night.</summary>
    FastChargerRepaired
}

/// <summary>
/// A single structured change to the world model, derived from a plain-language question.
/// </summary>
/// <remarks>
/// The set is deliberately closed. A challenge either maps onto one of these levers, in which
/// case the simulation genuinely reruns, or it is reported as unrecognised. Forkcast never
/// answers a what-if by generating prose about it.
/// </remarks>
public sealed record AssumptionOverride
{
    public required AssumptionKind Kind { get; init; }

    public required double Value { get; init; }

    /// <summary>How the change will be described in the interface.</summary>
    public required string Label { get; init; }

    /// <summary>The question as it was asked.</summary>
    public required string Question { get; init; }

    public bool Recognised => Kind != AssumptionKind.None;

    public static AssumptionOverride Unrecognised(string question) => new()
    {
        Kind = AssumptionKind.None,
        Value = 0,
        Label = "No supported assumption was recognised in this question",
        Question = question
    };
}
