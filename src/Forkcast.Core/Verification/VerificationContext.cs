using Forkcast.Core.Incidents;
using Forkcast.Core.Simulation;

namespace Forkcast.Core.Verification;

/// <summary>
/// Numbers a narrative may use that are not simulation results: fleet counts, the seed, the
/// trial count and other facts taken straight from the incident.
/// </summary>
/// <remarks>
/// Kept deliberately small. Anything not in here and not backed by a claim is treated as
/// invented, which is the behaviour we want: a generous allow-list would quietly defeat the
/// verifier.
/// </remarks>
public sealed record VerificationContext
{
    public required IReadOnlyDictionary<double, string> AllowedValues { get; init; }

    public static VerificationContext Empty { get; } = new()
    {
        AllowedValues = new Dictionary<double, string>()
    };

    public static VerificationContext FromIncident(Incident incident, SimulationOptions options)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(options);

        var words = incident.Vocabulary;
        var allowed = new Dictionary<double, string>
        {
            [incident.VehicleCount] = $"{words.UnitPlural} in scope",
            [incident.OperationalChargePointCount] = $"operational {words.ResourcePlural}",
            [incident.ChargePoints.Count] = $"{words.ResourcePlural} on site",
            [incident.FailedChargePointCount] = $"failed {words.ResourcePlural}",
            [incident.PriorityVehicleCount] = words.PriorityLabelPlural,
            [options.Seed] = "simulation seed",
            [options.TrialCount] = "simulated trials",
            [options.AtRiskProbabilityThreshold * 100.0] = "at-risk probability threshold",
            [incident.Constraints.AcArrayCapacityKw] = $"{words.CapacityPoolLabel} in {words.RateUnit}"
        };

        var acRating = incident.ChargePoints
            .Where(c => c is { Kind: ChargePointKind.DepotAc, IsOperational: true })
            .Sum(c => c.RatedPowerKw);
        if (acRating > 0)
        {
            allowed[acRating] = $"combined {words.ResourceSingular} rating in {words.RateUnit}";
        }

        return new VerificationContext { AllowedValues = allowed };
    }

    public bool TryDescribe(double value, out string reason)
    {
        foreach (var (allowed, description) in AllowedValues)
        {
            if (Math.Abs(allowed - value) < 1e-6)
            {
                reason = description;
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }
}
