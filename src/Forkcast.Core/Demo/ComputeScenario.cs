using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;

namespace Forkcast.Core.Demo;

/// <summary>
/// A second incident, in a domain with nothing to do with vehicles: a GPU cluster that has lost
/// cooling on two racks, with a regulatory reporting cut-off it still has to hit.
/// </summary>
/// <remarks>
/// <para>
/// This scenario exists to make one claim checkable rather than aspirational. Forkcast's engine
/// models a single shape — a queue of units, each needing a quantity delivered before its own
/// deadline, competing for resources whose combined throughput is capped — and this is that same
/// shape wearing different nouns.
/// </para>
/// <para>
/// Nothing in <c>Forkcast.Core.Simulation</c>, <c>Comparison</c>, <c>Verification</c> or
/// <c>Recommendations</c> was changed to support it. The engine's throughput is GPU-hours per hour
/// instead of kilowatts, its resources are worker slots instead of connectors, and its towed
/// battery is burst capacity in another region. The decision logic, the claim layer, the common
/// random numbers and the challenge levers are the same code paths the fleet scenario runs.
/// </para>
/// </remarks>
public static class ComputeScenario
{
    private static readonly TimeSpan SiteOffset = TimeSpan.FromHours(1);

    private static readonly DateOnly IncidentDate = new(2026, 7, 28);

    public const string NarrativeText =
        """
        At 17:20 two racks in our Slough compute hall dropped out after a chilled-water fault.

        24 overnight batch jobs must complete by 05:30 for the regulatory reporting cut-off.
        Ten GPU nodes remain available.
        Job progress currently ranges from 22% to 68%.
        Seven jobs feed regulated submissions and cannot slip.
        """;

    public static IncidentVocabulary Vocabulary { get; } = new()
    {
        DomainKey = "compute",
        DomainLabel = "GPU compute cluster",
        UnitSingular = "job",
        UnitPlural = "jobs",
        ResourceSingular = "GPU node",
        ResourcePlural = "GPU nodes",
        ConnectorNoun = "worker slot",
        LevelUnit = "GPU-hours",
        RateUnit = "GPU-hours per hour",
        DeadlineNoun = "cut-off",
        OnTimeMetricLabel = "on-time completions",
        PriorityLabelSingular = "regulated submission",
        PriorityLabelPlural = "regulated submissions",
        CapacityPoolLabel = "hall cooling envelope",
        BufferLabel = "burst capacity in the paired region",
        ShortfallLabel = "unfinished compute"
    };

    public static Incident Incident { get; } = BuildIncident();

    /// <summary>Do nothing differently: let the queue drain in its scheduled order.</summary>
    public static ResponsePlan PlanA { get; } = new()
    {
        Id = "plan-a",
        Name = "Hold the scheduled queue",
        Headline = "Leave the batch queue as scheduled and raise a cooling repair ticket.",
        Description =
            "Leave the overnight schedule untouched. Jobs keep their assigned nodes and run every "
            + "optional validation pass as normal. The three jobs pinned to the failed racks wait "
            + "for a worker slot to free up.",
        Actions =
        [
            "Keep the existing job order and node assignments",
            "Run every job through its full optional validation passes",
            "Re-queue jobs onto nodes as slots free up, in submission order",
            "Raise a chilled-water repair ticket for the day shift"
        ],
        ChargingPolicy = ChargingPolicy.KeepExistingSchedule,
        ChargeTargetPolicy = ChargeTargetPolicy.Full,
        ChargeMarginPct = 0.0,
        MobileBuffer = null,
        FixedInterventionCostGbp = 0.0
    };

    /// <summary>Act: reorder by deadline slack and buy capacity in the paired region.</summary>
    public static ResponsePlan PlanB { get; } = new()
    {
        Id = "plan-b",
        Name = "Reprioritise and burst to the paired region",
        Headline = "Reprioritise regulated submissions and burst to the paired region.",
        Description =
            "Re-sequence the queue so regulated submissions and the tightest deadlines run first, "
            + "stop each job at the checkpoint its submission actually requires, and open burst "
            + "capacity in the paired region, which is outside the failed hall's cooling envelope.",
        Actions =
        [
            "Re-sequence the queue by regulated submission, then by least deadline slack",
            "Stop each job at its required checkpoint plus a 3% margin rather than running every pass",
            "Open burst capacity in the paired region from 21:25",
            "Route the largest remaining workloads to the two burst worker slots"
        ],
        ChargingPolicy = ChargingPolicy.PriorityAndTightestMargin,
        ChargeTargetPolicy = ChargeTargetPolicy.RouteRequirementPlusMargin,
        ChargeMarginPct = 3.0,
        MobileBuffer = new MobileBufferOption
        {
            Outlets = 2,
            OutletPowerKw = 40.0,
            StoredEnergyKwh = 500.0,
            PlannedArrival = At(0, 21, 25),
            ArrivalDelayMeanMinutes = 0.0,
            ArrivalDelayStdDevMinutes = 15.0,
            CallOutCostGbp = 410.0,
            EnergyCostPerKwhGbp = 0.16
        },
        FixedInterventionCostGbp = 0.0
    };

    public static IReadOnlyList<ResponsePlan> Plans { get; } = [PlanA, PlanB];

    public static TimeSpan BufferLeadTime { get; } = PlanB.MobileBuffer!.PlannedArrival - Incident.DetectedAt;

    public static IReadOnlyList<ResponsePlan> PlansFor(Incident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        if (incident.DetectedAt == Incident.DetectedAt)
        {
            return Plans;
        }

        return
        [
            PlanA,
            PlanB with
            {
                MobileBuffer = PlanB.MobileBuffer! with
                {
                    PlannedArrival = incident.DetectedAt + BufferLeadTime
                }
            }
        ];
    }

    private static DateTimeOffset At(int dayOffset, int hour, int minute) => new DateTimeOffset(
        IncidentDate.Year, IncidentDate.Month, IncidentDate.Day, hour, minute, 0, SiteOffset)
        .AddDays(dayOffset);

    private static Incident BuildIncident() => new()
    {
        Id = "INC-2026-0728-02",
        Title = "Cooling fault in Slough compute hall",
        Narrative = NarrativeText,
        Site = "Slough compute hall",
        Vocabulary = Vocabulary,
        DetectedAt = At(0, 17, 20),
        DepartureDeadline = At(1, 5, 30),
        Fleet = BuildJobs(),
        ChargePoints = BuildNodes(),
        Constraints = BuildConstraints(),
        Tariff = BuildTariff(),
        Failures =
        [
            "Racks R7 and R8 offline — chilled-water loop fault CHW-221",
            "Three jobs with the largest remaining workloads were pinned to the failed racks",
            "Remaining capacity is ten GPU nodes for 24 queued jobs",
            "The hall cooling envelope caps the node array at 78 GPU-hours per hour"
        ]
    };

    private static IReadOnlyList<ChargePoint> BuildNodes()
    {
        var nodes = new List<ChargePoint>();
        for (var i = 1; i <= 10; i++)
        {
            nodes.Add(new ChargePoint
            {
                Id = $"GPU-{i:00}",
                Kind = ChargePointKind.DepotAc,
                RatedPowerKw = 11.0,
                IsOperational = true
            });
        }

        // The two failed racks were the dense ones — the reason the hall could clear its queue.
        for (var i = 7; i <= 8; i++)
        {
            nodes.Add(new ChargePoint
            {
                Id = $"RACK-R{i}",
                Kind = ChargePointKind.DcFast,
                RatedPowerKw = 96.0,
                IsOperational = false,
                FaultCode = "CHW-221",
                FaultSummary = "Chilled-water loop fault, rack powered down to protect the GPUs"
            });
        }

        return nodes;
    }

    private static DepotConstraints BuildConstraints() => new()
    {
        AcArrayCapacityKw = 78.0,
        PlugSwapBaseMinutes = 12.0,
        PlugSwapTailMeanMinutes = 8.0,
        PlugSwapMaxMinutes = 55.0,
        PreDepartureReadyMinutes = 12.0,
        DepartureJitterStdDevMinutes = 5.0,
        FaultRecoveryProbability = 0.2,
        FaultRecoveryWindowStart = At(0, 22, 30),
        FaultRecoveryWindowEnd = At(1, 3, 30),
        ChargePointPowerFactorMean = 0.93,
        ChargePointPowerFactorStdDev = 0.06,
        EnergyRequirementStdDev = 0.08,
        EnergyToleranceKwh = 0.5
    };

    /// <summary>Spot price for burstable capacity, which moves across the night.</summary>
    private static IReadOnlyList<TariffWindow> BuildTariff() =>
    [
        new() { Label = "Evening peak", From = At(0, 17, 20), To = At(0, 20, 0), PricePerKwhGbp = 0.295 },
        new() { Label = "Shoulder", From = At(0, 20, 0), To = At(0, 23, 0), PricePerKwhGbp = 0.208 },
        new() { Label = "Overnight trough", From = At(0, 23, 0), To = At(1, 5, 0), PricePerKwhGbp = 0.094 },
        new() { Label = "Morning ramp", From = At(1, 5, 0), To = At(1, 5, 30), PricePerKwhGbp = 0.243 }
    ];

    private static IReadOnlyList<Vehicle> BuildJobs() =>
    [
        Job("JOB-01", "Regulated: capital markets", 110, 29, 88, 60, true, 4, 10, "GPU-01"),
        Job("JOB-02", "Regulated: liquidity", 110, 22, 90, 60, true, 4, 15, "RACK-R7"),
        Job("JOB-03", "Regulated: credit risk", 140, 35, 86, 90, true, 4, 20, "GPU-02"),
        Job("JOB-04", "Regulated: stress test", 140, 24, 92, 90, true, 4, 30, "RACK-R7"),
        Job("JOB-05", "Regulated: counterparty", 110, 42, 84, 60, true, 4, 35, "GPU-03"),
        Job("JOB-06", "Regulated: market risk", 90, 31, 88, 50, true, 4, 40, "GPU-04"),
        Job("JOB-07", "Regulated: disclosure", 90, 47, 82, 50, true, 4, 45, "GPU-05"),
        Job("JOB-08", "Reconciliation A", 110, 38, 82, 60, false, 4, 50, "GPU-06"),
        Job("JOB-09", "Reconciliation B", 90, 63, 78, 50, false, 4, 55, "GPU-07"),
        Job("JOB-10", "Feature build A", 140, 26, 90, 90, false, 5, 0, "RACK-R8"),
        Job("JOB-11", "Feature build B", 140, 33, 88, 90, false, 5, 2, "GPU-08"),
        Job("JOB-12", "Retrain: pricing", 90, 55, 80, 50, false, 5, 4, "GPU-09"),
        Job("JOB-13", "Retrain: fraud", 110, 44, 83, 60, false, 5, 6, "GPU-10"),
        Job("JOB-14", "Backfill: ledger", 140, 30, 89, 90, false, 5, 8, "GPU-01"),
        Job("JOB-15", "Backfill: telemetry", 90, 68, 79, 50, false, 5, 10, "GPU-02"),
        Job("JOB-16", "Index rebuild", 110, 52, 81, 60, false, 5, 12, "GPU-03"),
        Job("JOB-17", "Embeddings refresh", 140, 40, 87, 90, false, 5, 14, "GPU-04"),
        Job("JOB-18", "Archive compaction", 90, 59, 78, 50, false, 5, 16, "GPU-05"),
        Job("JOB-19", "Report render A", 110, 46, 84, 60, false, 5, 18, "GPU-06"),
        Job("JOB-20", "Report render B", 140, 37, 90, 90, false, 5, 20, "GPU-07"),
        Job("JOB-21", "Sandbox refresh", 90, 61, 80, 50, false, 5, 22, "GPU-08"),
        Job("JOB-22", "Cost attribution", 110, 49, 83, 60, false, 5, 24, "GPU-09"),
        Job("JOB-23", "Data quality sweep", 90, 57, 79, 50, false, 5, 26, "GPU-10"),
        Job("JOB-24", "Nightly export", 140, 34, 88, 90, false, 5, 30, "GPU-01")
    ];

    private static Vehicle Job(
        string id,
        string route,
        double totalWork,
        double progressPct,
        double requiredPct,
        double burstRate,
        bool regulated,
        int deadlineHour,
        int deadlineMinute,
        string assignedNodeId) => new()
        {
            Id = id,
            Route = route,
            BatteryCapacityKwh = totalWork,
            InitialStateOfChargePct = progressPct,
            RequiredStateOfChargePct = requiredPct,
            MaxAcChargePowerKw = 11.0,
            MaxDcChargePowerKw = burstRate,
            IsPriorityRoute = regulated,
            ScheduledDeparture = At(1, deadlineHour, deadlineMinute),
            RosteredChargePointId = assignedNodeId
        };
}
