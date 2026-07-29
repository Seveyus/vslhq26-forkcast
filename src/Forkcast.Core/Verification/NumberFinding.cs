namespace Forkcast.Core.Verification;

/// <summary>
/// One number found in a piece of narrative text, and the verdict on it.
/// </summary>
/// <remarks>
/// The verifier's decision is only trustworthy if it can be inspected, so it reports every number
/// it looked at rather than only the ones it rejected. A reader can then see that the figures it
/// let through were let through for a stated reason.
/// </remarks>
public sealed record NumberFinding
{
    /// <summary>The token exactly as it appeared in the text, e.g. "12,400".</summary>
    public required string Token { get; init; }

    public required double Value { get; init; }

    /// <summary>Surrounding words, so the number can be located in the text.</summary>
    public required string Context { get; init; }

    public required bool Supported { get; init; }

    /// <summary>Id of the claim that backs it, when a claim does.</summary>
    public string? ClaimId { get; init; }

    /// <summary>Why it was allowed, or null when nothing allowed it.</summary>
    public string? Reason { get; init; }
}
