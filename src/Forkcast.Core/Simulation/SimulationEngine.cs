using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;

namespace Forkcast.Core.Simulation;

/// <summary>
/// Deterministic Monte Carlo engine for depot charging response plans.
/// </summary>
/// <remarks>
/// <para>
/// This is a decision-support model, not a physical simulation of a depot. It models charge
/// point occupancy, manual re-plug delays, static load management against the grid connection,
/// a towed battery buffer and a handful of uncertainty sources. It deliberately ignores
/// battery charge curves, thermal behaviour, cable losses and driver behaviour.
/// </para>
/// <para>
/// Its job is to rank two response plans and quantify the gap between them, with every number
/// reproducible from <see cref="SimulationOptions.Seed"/>.
/// </para>
/// </remarks>
public sealed class SimulationEngine
{
    private const double LoadCurveIntervalMinutes = 20.0;

    /// <summary>Nominal power used only to order the queue. Never used to compute an outcome.</summary>
    private const double QueueEstimatePowerKw = 11.0;

    /// <summary>How far ahead of the deadline a priority route is pulled in the queue.</summary>
    private const double PriorityQueueBonusMinutes = 150.0;

    public PlanOutcome Run(Incident incident, ResponsePlan plan, SimulationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(plan);

        var opts = (options ?? SimulationOptions.Default).Validated();
        var fleetSize = incident.Fleet.Count;
        var horizonMinutes = (incident.DepartureDeadline - incident.DetectedAt).TotalMinutes;

        var onTimeCounts = new int[fleetSize];
        var shortfallTotals = new double[fleetSize];
        var slackTotals = new double[fleetSize];
        var onTimeShare = new double[opts.TrialCount];

        var gridEnergyTotal = 0.0;
        var bufferEnergyTotal = 0.0;
        var energyCostTotal = 0.0;
        var bufferCostTotal = 0.0;
        var busyMinutesTotal = 0.0;
        var availableMinutesTotal = 0.0;
        var acLimitBindingTrials = 0;
        var bufferExhaustedTrials = 0;
        var neverPluggedTotal = 0.0;

        TrialResult? representative = null;
        var representativeDistance = double.MaxValue;
        var runningOnTimeMean = 0.0;

        for (var trial = 0; trial < opts.TrialCount; trial++)
        {
            var noise = TrialNoise.Sample(incident, opts.Seed, trial);
            var result = RunTrial(incident, plan, noise, horizonMinutes);

            var onTime = 0;
            for (var v = 0; v < fleetSize; v++)
            {
                if (result.OnTime[v])
                {
                    onTimeCounts[v]++;
                    onTime++;
                }

                shortfallTotals[v] += result.ShortfallKwh[v];
                slackTotals[v] += result.SlackMinutes[v];
            }

            onTimeShare[trial] = fleetSize == 0 ? 0.0 : onTime * 100.0 / fleetSize;
            gridEnergyTotal += result.GridEnergyKwh;
            bufferEnergyTotal += result.BufferEnergyKwh;
            energyCostTotal += result.GridEnergyCostGbp;
            bufferCostTotal += result.BufferEnergyCostGbp;
            busyMinutesTotal += result.BusyMinutes;
            availableMinutesTotal += result.AvailableMinutes;
            neverPluggedTotal += result.NeverPluggedCount;

            if (result.AcArrayLimitBinding)
            {
                acLimitBindingTrials++;
            }

            if (result.BufferExhausted)
            {
                bufferExhaustedTrials++;
            }

            // Keep the trial closest to the running mean so the timeline shows a typical night
            // rather than an arbitrary first sample. Trials whose fault outcome is the unlikely
            // one are skipped, otherwise the load curve can show a repaired fast charger that
            // most of the distribution never sees.
            runningOnTimeMean = ((runningOnTimeMean * trial) + onTimeShare[trial]) / (trial + 1);
            var typicalFaultState = incident.Constraints.FaultRecoveryProbability >= 0.5;
            var distance = Math.Abs(onTimeShare[trial] - runningOnTimeMean);
            if (noise.FaultRecovers == typicalFaultState && distance < representativeDistance)
            {
                representativeDistance = distance;
                representative = result;
            }
        }

        var trials = (double)opts.TrialCount;
        var vehicles = new List<VehicleOutcome>(fleetSize);
        var atRisk = 0;
        var priorityOnTime = 0.0;
        var priorityCount = 0;

        for (var v = 0; v < fleetSize; v++)
        {
            var vehicle = incident.Fleet[v];
            var probability = onTimeCounts[v] / trials;
            var vehicleAtRisk = probability < opts.AtRiskProbabilityThreshold;
            if (vehicleAtRisk)
            {
                atRisk++;
            }

            if (vehicle.IsPriorityRoute)
            {
                priorityCount++;
                priorityOnTime += probability;
            }

            vehicles.Add(new VehicleOutcome
            {
                VehicleId = vehicle.Id,
                Route = vehicle.Route,
                IsPriorityRoute = vehicle.IsPriorityRoute,
                OnTimeProbability = Round(probability, 4),
                ExpectedShortfallKwh = Round(shortfallTotals[v] / trials, 2),
                ExpectedSlackMinutes = Round(slackTotals[v] / trials, 1),
                IsAtRisk = vehicleAtRisk
            });
        }

        var sortedShare = onTimeShare.OrderBy(x => x).ToArray();
        var onTimePct = onTimeShare.Average();
        var expectedLate = fleetSize * (1.0 - (onTimePct / 100.0));
        var unmetEnergy = shortfallTotals.Sum() / trials;
        var energyCost = energyCostTotal / trials;
        var bufferEnergy = bufferEnergyTotal / trials;
        var bufferCost = bufferCostTotal / trials;
        var interventionCost = plan.FixedInterventionCostGbp
                               + (plan.MobileBuffer?.CallOutCostGbp ?? 0.0)
                               + bufferCost;
        var utilisation = availableMinutesTotal <= 0 ? 0.0 : busyMinutesTotal / availableMinutesTotal * 100.0;

        return new PlanOutcome
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            Seed = opts.Seed,
            TrialCount = opts.TrialCount,
            OnTimeDeparturePct = Round(onTimePct, 1),
            OnTimeDeparturePctP5 = Round(Percentile(sortedShare, 0.05), 1),
            OnTimeDeparturePctP95 = Round(Percentile(sortedShare, 0.95), 1),
            PriorityOnTimeDeparturePct = Round(priorityCount == 0 ? 100.0 : priorityOnTime / priorityCount * 100.0, 1),
            VehiclesAtRisk = atRisk,
            ExpectedLateVehicles = Round(expectedLate, 2),
            ExpectedUnmetEnergyKwh = Round(unmetEnergy, 1),
            ExpectedEnergyCostGbp = Round(energyCost, 2),
            ExpectedInterventionCostGbp = Round(interventionCost, 2),
            ExpectedOperationalCostGbp = Round(energyCost + interventionCost, 2),
            ExpectedBufferEnergyKwh = Round(bufferEnergy, 1),
            ExpectedGridEnergyKwh = Round(gridEnergyTotal / trials, 1),
            ChargePointUtilisationPct = Round(utilisation, 1),
            RiskLevel = ClassifyRisk(onTimePct, atRisk, fleetSize),
            CriticalConstraint = DescribeCriticalConstraint(
                incident,
                plan,
                utilisation,
                acLimitBindingTrials / trials,
                bufferExhaustedTrials / trials,
                neverPluggedTotal / trials,
                unmetEnergy),
            Vehicles = vehicles,
            LoadCurve = BuildLoadCurve(incident, representative, horizonMinutes)
        };
    }

    private static TrialResult RunTrial(
        Incident incident,
        ResponsePlan plan,
        TrialNoise noise,
        double horizonMinutes)
    {
        var fleetSize = incident.Fleet.Count;
        var resources = BuildResources(incident, plan, noise, horizonMinutes);
        var bufferBudget = new EnergyBudget(plan.MobileBuffer?.StoredEnergyKwh ?? 0.0);
        var acArrayCapacity = incident.Constraints.AcArrayCapacityKw;
        var tolerance = incident.Constraints.EnergyToleranceKwh;

        noise.ResetSwapCursor();

        var result = new TrialResult(fleetSize);
        var order = BuildQueue(incident, plan, noise);

        foreach (var index in order)
        {
            var vehicle = incident.Fleet[index];
            var factor = noise.EnergyFactor[index];
            var requiredEnergy = vehicle.RequiredEnergyKwh * factor;
            var targetEnergy = Math.Max(requiredEnergy, plan.TargetEnergyKwh(vehicle) * factor);
            var readyBy = (vehicle.ScheduledDeparture - incident.DetectedAt).TotalMinutes
                          - incident.Constraints.PreDepartureReadyMinutes
                          + noise.DepartureJitterMinutes[index];

            if (requiredEnergy <= tolerance)
            {
                // Already has the charge it needs. It still departs on time without a connector.
                result.OnTime[index] = true;
                result.SlackMinutes[index] = readyBy;
                continue;
            }

            var choice = ChooseResource(resources, vehicle, requiredEnergy, targetEnergy, readyBy, acArrayCapacity, bufferBudget, tolerance);
            if (choice is null)
            {
                // Every connector frees up only after this vehicle has already left the yard.
                result.ShortfallKwh[index] = requiredEnergy;
                result.SlackMinutes[index] = -Math.Abs(horizonMinutes);
                result.NeverPluggedCount++;
                continue;
            }

            var (resource, start, power) = choice.Value;
            var deliverable = Math.Min(targetEnergy, resource.RemainingEnergy(bufferBudget));
            var minutesToTarget = deliverable / power * 60.0;
            var sessionEnd = Math.Min(start + minutesToTarget, readyBy);
            var delivered = Math.Min(deliverable, power * (sessionEnd - start) / 60.0);

            var shortfall = Math.Max(0.0, requiredEnergy - delivered);
            var onTime = shortfall <= tolerance;

            result.OnTime[index] = onTime;
            result.ShortfallKwh[index] = shortfall;
            result.SlackMinutes[index] = onTime
                ? readyBy - (start + (requiredEnergy / power * 60.0))
                : -(shortfall / power * 60.0);

            if (resource.IsBuffer)
            {
                bufferBudget.Consume(delivered);
                result.BufferEnergyKwh += delivered;
                result.BufferEnergyCostGbp += delivered * (plan.MobileBuffer?.EnergyCostPerKwhGbp ?? 0.0);
            }
            else
            {
                result.GridEnergyKwh += delivered;
                var from = incident.DetectedAt.AddMinutes(start);
                var to = incident.DetectedAt.AddMinutes(sessionEnd);
                result.GridEnergyCostGbp += delivered * incident.AveragePricePerKwh(from, to);
            }

            result.BusyMinutes += sessionEnd - start;
            resource.FreeAtMinute = sessionEnd + noise.NextSwapLatency();
            result.Sessions.Add(new Session(vehicle.Id, resource.Id, resource.IsBuffer, start, sessionEnd, power));
        }

        result.AvailableMinutes = resources.Sum(r => Math.Max(0.0, horizonMinutes - r.AvailableFromMinute));
        result.AcArrayLimitBinding = resources
            .Where(r => r.Kind == ChargePointKind.DepotAc)
            .Sum(r => r.RatedPowerKw) > acArrayCapacity + 0.001;
        result.BufferExhausted = plan.MobileBuffer is not null && bufferBudget.Remaining <= tolerance;

        return result;
    }

    /// <summary>
    /// Greedy list scheduling: give the vehicle the connector that meets its route requirement
    /// soonest. Connectors that cannot meet the requirement at all are ranked behind those that can.
    /// </summary>
    private static (Resource Resource, double Start, double Power)? ChooseResource(
        IReadOnlyList<Resource> resources,
        Vehicle vehicle,
        double requiredEnergy,
        double targetEnergy,
        double readyBy,
        double acArrayCapacityKw,
        EnergyBudget bufferBudget,
        double tolerance)
    {
        Resource? best = null;
        var bestStart = 0.0;
        var bestPower = 0.0;
        var bestScore = double.MaxValue;

        foreach (var resource in resources)
        {
            var start = Math.Max(resource.FreeAtMinute, resource.AvailableFromMinute);
            if (start >= readyBy)
            {
                continue;
            }

            var power = resource.EffectivePowerFor(vehicle, resources, acArrayCapacityKw, start);
            if (power <= 0.01)
            {
                continue;
            }

            var deliverable = Math.Min(targetEnergy, resource.RemainingEnergy(bufferBudget));
            double score;
            if (deliverable + tolerance >= requiredEnergy)
            {
                score = start + (requiredEnergy / power * 60.0);
            }
            else
            {
                // Cannot satisfy the route: rank behind every viable option, best effort first.
                score = 1_000_000.0 + ((requiredEnergy - deliverable) * 100.0) + start;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = resource;
                bestStart = start;
                bestPower = power;
            }
        }

        return best is null ? null : (best, bestStart, bestPower);
    }

    private static List<Resource> BuildResources(
        Incident incident,
        ResponsePlan plan,
        TrialNoise noise,
        double horizonMinutes)
    {
        var resources = new List<Resource>();

        for (var i = 0; i < incident.ChargePoints.Count; i++)
        {
            var point = incident.ChargePoints[i];
            double availableFrom;
            if (point.IsOperational)
            {
                availableFrom = 0.0;
            }
            else if (noise.FaultRecovers && noise.FaultRecoveryMinute < horizonMinutes)
            {
                availableFrom = noise.FaultRecoveryMinute;
            }
            else
            {
                continue;
            }

            resources.Add(new Resource
            {
                Id = point.Id,
                Kind = point.Kind,
                RatedPowerKw = point.RatedPowerKw,
                PowerFactor = noise.PowerFactor[i],
                AvailableFromMinute = availableFrom,
                FreeAtMinute = availableFrom
            });
        }

        if (plan.MobileBuffer is { } buffer)
        {
            var delay = buffer.ArrivalDelayMeanMinutes
                        + (noise.BufferArrivalDelayMinutes * buffer.ArrivalDelayStdDevMinutes);
            var arrival = (buffer.PlannedArrival - incident.DetectedAt).TotalMinutes + Math.Max(0.0, delay);

            for (var outlet = 0; outlet < buffer.Outlets; outlet++)
            {
                var slot = incident.ChargePoints.Count + outlet;
                resources.Add(new Resource
                {
                    Id = $"BUF-{outlet + 1:00}",
                    Kind = ChargePointKind.MobileBuffer,
                    RatedPowerKw = buffer.OutletPowerKw,
                    PowerFactor = noise.PowerFactor[slot],
                    AvailableFromMinute = arrival,
                    FreeAtMinute = arrival
                });
            }
        }

        return resources;
    }

    private static List<int> BuildQueue(Incident incident, ResponsePlan plan, TrialNoise noise)
    {
        var indices = Enumerable.Range(0, incident.Fleet.Count).ToList();

        if (plan.ChargingPolicy == ChargingPolicy.KeepExistingSchedule)
        {
            var downChargePoints = incident.ChargePoints
                .Where(c => !c.IsOperational)
                .Select(c => c.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Stable partition: the rota is untouched, so vehicles rostered onto a dead
            // connector simply drop to the back of the yard list.
            return indices
                .OrderBy(i => downChargePoints.Contains(incident.Fleet[i].RosteredChargePointId) ? 1 : 0)
                .ToList();
        }

        return indices
            .OrderBy(i =>
            {
                var vehicle = incident.Fleet[i];
                var readyBy = (vehicle.ScheduledDeparture - incident.DetectedAt).TotalMinutes
                              - incident.Constraints.PreDepartureReadyMinutes
                              + noise.DepartureJitterMinutes[i];
                var estimatedMinutes = plan.TargetEnergyKwh(vehicle) / QueueEstimatePowerKw * 60.0;
                var slack = readyBy - estimatedMinutes;
                return vehicle.IsPriorityRoute ? slack - PriorityQueueBonusMinutes : slack;
            })
            .ToList();
    }

    private static RiskLevel ClassifyRisk(double onTimePct, int atRisk, int fleetSize)
    {
        var atRiskShare = fleetSize == 0 ? 0.0 : atRisk / (double)fleetSize;
        return onTimePct switch
        {
            >= 92.0 when atRiskShare <= 0.10 => RiskLevel.Low,
            >= 80.0 when atRiskShare <= 0.25 => RiskLevel.Medium,
            >= 55.0 => RiskLevel.High,
            _ => RiskLevel.Critical
        };
    }

    private static string DescribeCriticalConstraint(
        Incident incident,
        ResponsePlan plan,
        double utilisationPct,
        double acLimitBindingShare,
        double bufferExhaustedShare,
        double expectedNeverPlugged,
        double unmetEnergyKwh)
    {
        var words = incident.Vocabulary;

        if (expectedNeverPlugged >= 1.0)
        {
            return $"{Capitalise(words.ConnectorNoun)} availability — {expectedNeverPlugged:0.#} "
                   + $"{words.UnitPlural} never reach a free {words.ConnectorNoun} before their "
                   + $"{words.DeadlineNoun} ({incident.OperationalChargePointCount} of "
                   + $"{incident.ChargePoints.Count} {words.ResourcePlural} operational)";
        }

        if (plan.MobileBuffer is { } buffer && bufferExhaustedShare >= 0.5 && unmetEnergyKwh > 1.0)
        {
            return $"{Capitalise(words.BufferLabel)} budget — {buffer.StoredEnergyKwh:0} "
                   + $"{words.LevelUnit} is fully drawn in {bufferExhaustedShare * 100:0} percent of runs";
        }

        if (acLimitBindingShare >= 0.5 && unmetEnergyKwh > 1.0)
        {
            var poolRating = incident.ChargePoints
                .Where(c => c is { Kind: ChargePointKind.DepotAc, IsOperational: true })
                .Sum(c => c.RatedPowerKw);
            return $"{Capitalise(words.CapacityPoolLabel)} — {poolRating:0} {words.RateUnit} of "
                   + $"{words.ResourcePlural} sharing {incident.Constraints.AcArrayCapacityKw:0} "
                   + $"{words.RateUnit} of available capacity";
        }

        if (utilisationPct >= 85.0)
        {
            return $"{Capitalise(words.ResourceSingular)} hours — {words.ResourcePlural} are "
                   + $"occupied {utilisationPct:0} percent of the window";
        }

        return $"{Capitalise(words.DeadlineNoun)} window — {incident.PriorityVehicleCount} "
               + $"{words.PriorityLabelPlural} reach their deadline before "
               + $"{incident.DepartureDeadline:HH\\:mm}";
    }

    private static IReadOnlyList<LoadSample> BuildLoadCurve(
        Incident incident,
        TrialResult? trial,
        double horizonMinutes)
    {
        if (trial is null)
        {
            return [];
        }

        var samples = new List<LoadSample>();
        for (var minute = 0.0; minute <= horizonMinutes; minute += LoadCurveIntervalMinutes)
        {
            var gridKw = 0.0;
            var bufferKw = 0.0;
            var charging = 0;
            var ready = 0;

            foreach (var session in trial.Sessions)
            {
                if (minute >= session.StartMinute && minute < session.EndMinute)
                {
                    charging++;
                    if (session.IsBuffer)
                    {
                        bufferKw += session.PowerKw;
                    }
                    else
                    {
                        gridKw += session.PowerKw;
                    }
                }
                else if (minute >= session.EndMinute)
                {
                    ready++;
                }
            }

            samples.Add(new LoadSample
            {
                At = incident.DetectedAt.AddMinutes(minute),
                GridPowerKw = Round(gridKw, 1),
                BufferPowerKw = Round(bufferKw, 1),
                VehiclesCharging = charging,
                VehiclesReady = ready
            });
        }

        return samples;
    }

    private static double Percentile(IReadOnlyList<double> sorted, double q)
    {
        if (sorted.Count == 0)
        {
            return 0.0;
        }

        var position = q * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }

        var weight = position - lower;
        return (sorted[lower] * (1 - weight)) + (sorted[upper] * weight);
    }

    private static double Round(double value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    private static string Capitalise(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private sealed class Resource
    {
        public required string Id { get; init; }

        public required ChargePointKind Kind { get; init; }

        public required double RatedPowerKw { get; init; }

        public required double PowerFactor { get; init; }

        public required double AvailableFromMinute { get; init; }

        public double FreeAtMinute { get; set; }

        public bool IsBuffer => Kind == ChargePointKind.MobileBuffer;

        public bool DrawsFromGrid => Kind is ChargePointKind.DepotAc or ChargePointKind.DcFast;

        public double RemainingEnergy(EnergyBudget budget) =>
            IsBuffer ? budget.Remaining : double.PositiveInfinity;

        /// <summary>
        /// Effective delivered power. Depot AC boxes are limited by the vehicle onboard charger,
        /// DC connectors by the vehicle DC limit, and the AC array as a whole by the site's
        /// static load management once its combined rating exceeds the capacity left over after
        /// the site base load. The DC fast charger has its own feeder; the towed battery unit is
        /// battery fed and so bypasses the site connection entirely.
        /// </summary>
        public double EffectivePowerFor(
            Vehicle vehicle,
            IReadOnlyList<Resource> all,
            double acArrayCapacityKw,
            double atMinute)
        {
            var vehicleLimit = Kind == ChargePointKind.DepotAc
                ? vehicle.MaxAcChargePowerKw
                : vehicle.MaxDcChargePowerKw;

            var power = Math.Min(RatedPowerKw, vehicleLimit) * PowerFactor;
            if (Kind != ChargePointKind.DepotAc)
            {
                return power;
            }

            var arrayRating = 0.0;
            foreach (var other in all)
            {
                if (other.Kind == ChargePointKind.DepotAc && other.AvailableFromMinute <= atMinute)
                {
                    arrayRating += other.RatedPowerKw;
                }
            }

            if (arrayRating <= acArrayCapacityKw || arrayRating <= 0)
            {
                return power;
            }

            return power * (acArrayCapacityKw / arrayRating);
        }
    }

    private sealed class EnergyBudget(double initial)
    {
        public double Remaining { get; private set; } = initial;

        public void Consume(double kwh) => Remaining = Math.Max(0.0, Remaining - kwh);
    }

    private readonly record struct Session(
        string VehicleId,
        string ResourceId,
        bool IsBuffer,
        double StartMinute,
        double EndMinute,
        double PowerKw);

    private sealed class TrialResult(int fleetSize)
    {
        public bool[] OnTime { get; } = new bool[fleetSize];

        public double[] ShortfallKwh { get; } = new double[fleetSize];

        public double[] SlackMinutes { get; } = new double[fleetSize];

        public List<Session> Sessions { get; } = [];

        public double GridEnergyKwh { get; set; }

        public double BufferEnergyKwh { get; set; }

        public double GridEnergyCostGbp { get; set; }

        public double BufferEnergyCostGbp { get; set; }

        public double BusyMinutes { get; set; }

        public double AvailableMinutes { get; set; }

        public int NeverPluggedCount { get; set; }

        public bool AcArrayLimitBinding { get; set; }

        public bool BufferExhausted { get; set; }
    }
}
