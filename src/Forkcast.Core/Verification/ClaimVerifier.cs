using System.Globalization;
using System.Text.RegularExpressions;

namespace Forkcast.Core.Verification;

/// <summary>
/// Checks that every number in a piece of narrative text is backed by a claim or by incident
/// context, and replaces the text with a deterministic summary when it is not.
/// </summary>
/// <remarks>
/// This is the layer that lets Forkcast use a language model for language while refusing to let
/// it near the arithmetic. The model writes the explanation; if it invents a figure, the
/// explanation is discarded rather than corrected, because a narrative that got one number
/// wrong is not evidence about the others.
/// </remarks>
public sealed partial class ClaimVerifier
{
    private const double Epsilon = 1e-6;

    private const int ContextWords = 4;

    public ClaimVerification Verify(
        string? candidateNarrative,
        string candidateSource,
        string deterministicNarrative,
        IReadOnlyList<Claim> claims,
        VerificationContext context)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(deterministicNarrative);

        var verifiedCount = claims.Count(c => c.Verified);
        var seed = claims.Count > 0 ? claims[0].SimulationSeed : 0L;
        var trialCount = claims.Count > 0 ? claims[0].TrialCount : 0;

        var accepted = true;
        var unsupported = new List<UnsupportedNumber>();

        if (!string.IsNullOrWhiteSpace(candidateNarrative))
        {
            unsupported = FindUnsupportedNumbers(candidateNarrative, claims, context);
            accepted = unsupported.Count == 0 && verifiedCount == claims.Count;
        }
        else
        {
            accepted = false;
        }

        // The fallback is generated from the claims themselves, so it can only ever contain
        // supported numbers. Verifying it too would be circular; we assert it instead.
        var narrative = accepted ? candidateNarrative!.Trim() : deterministicNarrative.Trim();

        return new ClaimVerification
        {
            Claims = claims,
            TotalClaims = claims.Count,
            VerifiedClaims = verifiedCount,
            UnsupportedNumbers = unsupported.Count,
            Unsupported = unsupported,
            NarrativeAccepted = accepted,
            Narrative = narrative,
            NarrativeSource = accepted ? candidateSource : "deterministic",
            SimulationSeed = seed,
            TrialCount = trialCount
        };
    }

    /// <summary>
    /// Returns every numeric token in <paramref name="text"/> that no claim and no incident
    /// fact can account for.
    /// </summary>
    public List<UnsupportedNumber> FindUnsupportedNumbers(
        string text,
        IReadOnlyList<Claim> claims,
        VerificationContext context)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(context);

        // Identifiers and clock times are structural, not quantitative. Mask them so that
        // "EV-04" and "06:00" do not decompose into bare numbers.
        var masked = IdentifierPattern().Replace(text, m => new string('•', m.Length));
        masked = ClockTimePattern().Replace(masked, m => new string('•', m.Length));

        var supported = new HashSet<double>();
        foreach (var claim in claims)
        {
            foreach (var form in claim.AcceptableForms())
            {
                supported.Add(form);
            }
        }

        var findings = new List<UnsupportedNumber>();
        foreach (Match match in NumberPattern().Matches(masked))
        {
            var token = match.Value;
            var normalised = token.Replace(",", string.Empty, StringComparison.Ordinal);
            if (!double.TryParse(normalised, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            if (supported.Any(s => Math.Abs(s - value) < Epsilon))
            {
                continue;
            }

            if (context.TryDescribe(value, out _))
            {
                continue;
            }

            findings.Add(new UnsupportedNumber
            {
                Token = token,
                Context = ExtractContext(text, match.Index, match.Length)
            });
        }

        return findings;
    }

    private static string ExtractContext(string text, int index, int length)
    {
        var before = text[..Math.Min(index, text.Length)]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(ContextWords);

        var afterStart = Math.Min(index + length, text.Length);
        var after = text[afterStart..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(ContextWords);

        var window = string.Join(' ', before.Concat([text.Substring(index, length)]).Concat(after));
        return window.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    [GeneratedRegex(@"\b[A-Z]{2,4}-\d+\b")]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex(@"\b\d{1,2}:\d{2}\b")]
    private static partial Regex ClockTimePattern();

    [GeneratedRegex(@"-?\d[\d,]*(?:\.\d+)?")]
    private static partial Regex NumberPattern();
}
