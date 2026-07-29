using Forkcast.Core.Incidents;

namespace Forkcast.Core.Simulation;

/// <summary>
/// Every random quantity a single trial needs, drawn up front in a fixed order.
/// </summary>
/// <remarks>
/// This is the common random numbers technique. Both response plans are evaluated against the
/// *same* sampled night, so the difference between them reflects the plans rather than
/// sampling noise. It also means a plan can never look better by accident, which matters when
/// the output is a recommendation somebody has to act on.
/// </remarks>
internal sealed class TrialNoise
{
    private int _swapCursor;

    private TrialNoise(
        double[] energyFactor,
        double[] departureJitterMinutes,
        double[] powerFactor,
        double[] swapLatencyMinutes,
        bool faultRecovers,
        double faultRecoveryMinute,
        double bufferArrivalDelayMinutes)
    {
        EnergyFactor = energyFactor;
        DepartureJitterMinutes = departureJitterMinutes;
        PowerFactor = powerFactor;
        SwapLatencyMinutes = swapLatencyMinutes;
        FaultRecovers = faultRecovers;
        FaultRecoveryMinute = faultRecoveryMinute;
        BufferArrivalDelayMinutes = bufferArrivalDelayMinutes;
    }

    /// <summary>Per-vehicle multiplier on the energy the route actually needs.</summary>
    public double[] EnergyFactor { get; }

    /// <summary>Per-vehicle departure time jitter, in minutes.</summary>
    public double[] DepartureJitterMinutes { get; }

    /// <summary>Per-slot effective power derating. Slot indices are stable across plans.</summary>
    public double[] PowerFactor { get; }

    private double[] SwapLatencyMinutes { get; }

    public bool FaultRecovers { get; }

    /// <summary>Minutes after detection at which the failed charge point comes back.</summary>
    public double FaultRecoveryMinute { get; }

    public double BufferArrivalDelayMinutes { get; }

    /// <summary>Number of charge point slots reserved beyond the site's own charge points.</summary>
    public const int SpareSlots = 8;

    public void ResetSwapCursor() => _swapCursor = 0;

    /// <summary>Next manual re-plug delay from the pre-drawn pool.</summary>
    public double NextSwapLatency()
    {
        var value = SwapLatencyMinutes[_swapCursor % SwapLatencyMinutes.Length];
        _swapCursor++;
        return value;
    }

    public static TrialNoise Sample(Incident incident, long seed, int trialIndex)
    {
        var random = new DeterministicRandom(DeterministicRandom.DeriveSeed(seed, "trial", trialIndex));
        var constraints = incident.Constraints;
        var fleetSize = incident.Fleet.Count;
        var slotCount = incident.ChargePoints.Count + SpareSlots;

        var energyFactor = new double[fleetSize];
        for (var i = 0; i < fleetSize; i++)
        {
            energyFactor[i] = random.NextGaussian(1.0, constraints.EnergyRequirementStdDev, 0.82, 1.22);
        }

        var departureJitter = new double[fleetSize];
        for (var i = 0; i < fleetSize; i++)
        {
            departureJitter[i] = random.NextGaussian(
                0.0, constraints.DepartureJitterStdDevMinutes, -25.0, 25.0);
        }

        var powerFactor = new double[slotCount];
        for (var i = 0; i < slotCount; i++)
        {
            powerFactor[i] = random.NextGaussian(
                constraints.ChargePointPowerFactorMean,
                constraints.ChargePointPowerFactorStdDev,
                0.72,
                1.0);
        }

        // Generous pool: a night never needs more swaps than one per vehicle plus one per slot.
        var swapLatency = new double[fleetSize + slotCount];
        for (var i = 0; i < swapLatency.Length; i++)
        {
            var latency = constraints.PlugSwapBaseMinutes
                          + random.NextExponential(constraints.PlugSwapTailMeanMinutes);
            swapLatency[i] = Math.Min(latency, constraints.PlugSwapMaxMinutes);
        }

        var faultRecovers = random.NextBool(constraints.FaultRecoveryProbability);
        var recoveryStart = (constraints.FaultRecoveryWindowStart - incident.DetectedAt).TotalMinutes;
        var recoveryEnd = (constraints.FaultRecoveryWindowEnd - incident.DetectedAt).TotalMinutes;
        var faultRecoveryMinute = random.NextDouble(recoveryStart, recoveryEnd);

        // Drawn unconditionally so the stream stays aligned whether or not a plan uses a buffer.
        var bufferDelay = random.NextGaussian(0.0, 1.0);

        return new TrialNoise(
            energyFactor,
            departureJitter,
            powerFactor,
            swapLatency,
            faultRecovers,
            faultRecoveryMinute,
            bufferDelay);
    }
}
