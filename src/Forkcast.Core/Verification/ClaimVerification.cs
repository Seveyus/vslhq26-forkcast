namespace Forkcast.Core.Verification;

/// <summary>A number found in narrative text that no claim supports.</summary>
public sealed record UnsupportedNumber
{
    public required string Token { get; init; }

    /// <summary>Surrounding words, so a reviewer can see where the number came from.</summary>
    public required string Context { get; init; }
}

/// <summary>
/// The outcome of checking a piece of narrative text against the claim set.
/// </summary>
public sealed record ClaimVerification
{
    public required IReadOnlyList<Claim> Claims { get; init; }

    public required int TotalClaims { get; init; }

    public required int VerifiedClaims { get; init; }

    public required int UnsupportedNumbers { get; init; }

    public required IReadOnlyList<UnsupportedNumber> Unsupported { get; init; }

    /// <summary>False when the candidate narrative was rejected and replaced.</summary>
    public required bool NarrativeAccepted { get; init; }

    /// <summary>The text that is safe to display. Never contains an unsupported number.</summary>
    public required string Narrative { get; init; }

    /// <summary>"azure-openai" or "deterministic".</summary>
    public required string NarrativeSource { get; init; }

    public required long SimulationSeed { get; init; }

    public required int TrialCount { get; init; }

    public bool AllClaimsVerified => VerifiedClaims == TotalClaims;
}
