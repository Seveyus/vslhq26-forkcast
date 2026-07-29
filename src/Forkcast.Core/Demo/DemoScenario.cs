using Forkcast.Core.Incidents;
using Forkcast.Core.Plans;

namespace Forkcast.Core.Demo;

/// <summary>
/// The preloaded demonstration incident: a charger failure at an electric delivery depot.
/// </summary>
/// <remarks>
/// The fleet is synthetic but fixed, so anyone running Forkcast with the published seed gets
/// the published numbers. Forkcast itself is not fleet specific — this scenario is one worked
/// example of the general shape "constrained resource, hard deadline, competing responses".
/// </remarks>
public static class DemoScenario
{
    private static readonly TimeSpan SiteOffset = TimeSpan.FromHours(1); // Europe/London, summer

    private static readonly DateOnly IncidentDate = new(2026, 7, 28);

    public const string NarrativeText =
        """
        At 18:40 the fast charger at our Reading delivery depot failed with a rectifier fault.

        20 electric delivery vehicles must depart by 06:00 tomorrow morning.
        Eight AC charge points remain available.
        Current battery levels range from 24% to 71%.
        Six vehicles are assigned to priority routes that leave first.
        """;

    public static Incident Incident { get; } = BuildIncident();

    /// <summary>Do nothing differently: keep the overnight rota exactly as it was planned.</summary>
    public static ResponsePlan PlanA { get; } = new()
    {
        Id = "plan-a",
        Name = "Continue current schedule",
        Headline = "Leave the overnight rota unchanged and repair the fast charger in the morning.",
        Description =
            "Leave the overnight rota untouched. Vehicles keep their rostered connectors and "
            + "charge to full in the usual order. The three vehicles rostered onto the failed "
            + "fast charger wait for a connector to free up.",
        Actions =
        [
            "Keep the existing charge rota and connector assignments",
            "Charge every vehicle to 100% as normal",
            "Re-plug vehicles as connectors free up, in yard list order",
            "Raise a repair callout for the fast charger on the morning shift"
        ],
        ChargingPolicy = ChargingPolicy.KeepExistingSchedule,
        ChargeTargetPolicy = ChargeTargetPolicy.Full,
        ChargeMarginPct = 0.0,
        MobileBuffer = null,
        FixedInterventionCostGbp = 0.0
    };

    /// <summary>Act: reorder the yard queue by urgency and bring in a towed battery unit.</summary>
    public static ResponsePlan PlanB { get; } = new()
    {
        Id = "plan-b",
        Name = "Reprioritise and add temporary battery buffer",
        Headline = "Reprioritise priority-route vehicles and activate the temporary battery buffer.",
        Description =
            "Re-sequence the yard queue so priority routes and the tightest departures charge "
            + "first, stop charging past what each route needs, and bring in a towed battery "
            + "unit whose two DC outlets are battery fed and so bypass the site capacity limit.",
        Actions =
        [
            "Re-sequence the yard queue by priority route, then by least schedule slack",
            "Charge each vehicle to its route requirement plus a 3% margin, then move the connector on",
            "Call out the towed 420 kWh battery unit for 22:45 arrival",
            "Route the largest energy deficits to the two 36 kW DC buffer outlets"
        ],
        ChargingPolicy = ChargingPolicy.PriorityAndTightestMargin,
        ChargeTargetPolicy = ChargeTargetPolicy.RouteRequirementPlusMargin,
        ChargeMarginPct = 3.0,
        MobileBuffer = new MobileBufferOption
        {
            Outlets = 2,
            OutletPowerKw = 36.0,
            StoredEnergyKwh = 420.0,
            PlannedArrival = At(0, 22, 45),
            ArrivalDelayMeanMinutes = 0.0,
            ArrivalDelayStdDevMinutes = 18.0,
            CallOutCostGbp = 330.0,
            EnergyCostPerKwhGbp = 0.14
        },
        FixedInterventionCostGbp = 0.0
    };

    public static IReadOnlyList<ResponsePlan> Plans { get; } = [PlanA, PlanB];

    /// <summary>How long after detection the towed battery unit can realistically be on site.</summary>
    public static TimeSpan BufferLeadTime { get; } = PlanB.MobileBuffer!.PlannedArrival - Incident.DetectedAt;

    /// <summary>
    /// Rebuilds the two response plans against a possibly modified incident, keeping the call-out
    /// lead time rather than the absolute arrival clock time.
    /// </summary>
    public static IReadOnlyList<ResponsePlan> PlansFor(Incident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        if (incident.DetectedAt == Incident.DetectedAt)
        {
            return Plans;
        }

        var buffer = PlanB.MobileBuffer! with
        {
            PlannedArrival = incident.DetectedAt + BufferLeadTime
        };

        return [PlanA, PlanB with { MobileBuffer = buffer }];
    }

    private static DateTimeOffset At(int dayOffset, int hour, int minute) => new DateTimeOffset(
        IncidentDate.Year,
        IncidentDate.Month,
        IncidentDate.Day,
        hour,
        minute,
        0,
        SiteOffset).AddDays(dayOffset);

    private static Incident BuildIncident() => new()
    {
        Id = "INC-2026-0728-01",
        Title = "Fast charger failure at Reading delivery depot",
        Narrative = NarrativeText,
        Site = "Reading delivery depot",
        DetectedAt = At(0, 18, 40),
        DepartureDeadline = At(1, 6, 0),
        Fleet = BuildFleet(),
        ChargePoints = BuildChargePoints(),
        Constraints = BuildConstraints(),
        Tariff = BuildTariff(),
        Failures =
        [
            "CP-09 (150 kW DC fast charger) offline — rectifier fault E-4412",
            "Three vehicles with the largest energy deficits were rostered onto CP-09",
            "Remaining capacity is eight 11 kW AC charge points for 20 vehicles",
            "The AC array shares 74 kW of site capacity, so each post delivers about 9 kW"
        ]
    };

    private static IReadOnlyList<ChargePoint> BuildChargePoints()
    {
        var points = new List<ChargePoint>();
        for (var i = 1; i <= 8; i++)
        {
            points.Add(new ChargePoint
            {
                Id = $"CP-{i:00}",
                Kind = ChargePointKind.DepotAc,
                RatedPowerKw = 11.0,
                IsOperational = true
            });
        }

        points.Add(new ChargePoint
        {
            Id = "CP-09",
            Kind = ChargePointKind.DcFast,
            RatedPowerKw = 150.0,
            IsOperational = false,
            FaultCode = "E-4412",
            FaultSummary = "Rectifier module failure, no output on either connector"
        });

        return points;
    }

    private static DepotConstraints BuildConstraints() => new()
    {
        AcArrayCapacityKw = 74.0,
        PlugSwapBaseMinutes = 18.0,
        PlugSwapTailMeanMinutes = 10.0,
        PlugSwapMaxMinutes = 75.0,
        PreDepartureReadyMinutes = 15.0,
        DepartureJitterStdDevMinutes = 6.0,
        FaultRecoveryProbability = 0.15,
        FaultRecoveryWindowStart = At(0, 23, 0),
        FaultRecoveryWindowEnd = At(1, 4, 0),
        ChargePointPowerFactorMean = 0.94,
        ChargePointPowerFactorStdDev = 0.05,
        EnergyRequirementStdDev = 0.06,
        EnergyToleranceKwh = 0.5
    };

    private static IReadOnlyList<TariffWindow> BuildTariff() =>
    [
        new() { Label = "Evening peak", From = At(0, 18, 40), To = At(0, 20, 0), PricePerKwhGbp = 0.318 },
        new() { Label = "Shoulder", From = At(0, 20, 0), To = At(0, 23, 30), PricePerKwhGbp = 0.241 },
        new() { Label = "Overnight", From = At(0, 23, 30), To = At(1, 5, 30), PricePerKwhGbp = 0.112 },
        new() { Label = "Morning ramp", From = At(1, 5, 30), To = At(1, 6, 0), PricePerKwhGbp = 0.276 }
    ];

    private static IReadOnlyList<Vehicle> BuildFleet() =>
    [
        Van("EV-01", "Priority North", 110, 31, 88, 11, 60, true, 4, 40, "CP-01"),
        Van("EV-02", "Priority Central", 110, 24, 90, 11, 60, true, 4, 50, "CP-09"),
        Van("EV-03", "Priority East", 140, 38, 86, 11, 90, true, 4, 55, "CP-02"),
        Van("EV-04", "Priority Airport", 140, 27, 92, 11, 90, true, 5, 5, "CP-09"),
        Van("EV-05", "Priority South", 110, 45, 84, 11, 60, true, 5, 10, "CP-03"),
        Van("EV-06", "Priority West", 90, 33, 88, 11, 50, true, 5, 15, "CP-04"),
        Van("EV-07", "Urban A", 90, 52, 80, 11, 50, false, 5, 20, "CP-05"),
        Van("EV-08", "Urban B", 110, 41, 82, 11, 60, false, 5, 23, "CP-06"),
        Van("EV-09", "Urban C", 90, 66, 78, 11, 50, false, 5, 26, "CP-07"),
        Van("EV-10", "Regional A", 140, 29, 90, 11, 90, false, 5, 30, "CP-09"),
        Van("EV-11", "Regional B", 140, 36, 88, 11, 90, false, 5, 33, "CP-08"),
        Van("EV-12", "Urban D", 90, 58, 80, 11, 50, false, 5, 36, "CP-01"),
        Van("EV-13", "Urban E", 110, 47, 83, 11, 60, false, 5, 39, "CP-02"),
        Van("EV-14", "Regional C", 140, 34, 89, 11, 90, false, 5, 42, "CP-03"),
        Van("EV-15", "Urban F", 90, 71, 79, 11, 50, false, 5, 45, "CP-04"),
        Van("EV-16", "Urban G", 110, 55, 81, 11, 60, false, 5, 48, "CP-05"),
        Van("EV-17", "Regional D", 140, 43, 87, 11, 90, false, 5, 51, "CP-06"),
        Van("EV-18", "Urban H", 90, 62, 78, 11, 50, false, 5, 54, "CP-07"),
        Van("EV-19", "Urban I", 110, 49, 84, 11, 60, false, 5, 57, "CP-08"),
        Van("EV-20", "Regional E", 140, 40, 90, 11, 90, false, 6, 0, "CP-01")
    ];

    private static Vehicle Van(
        string id,
        string route,
        double capacityKwh,
        double initialSocPct,
        double requiredSocPct,
        double acLimitKw,
        double dcLimitKw,
        bool priority,
        int departureHour,
        int departureMinute,
        string rosteredChargePointId) => new()
        {
            Id = id,
            Route = route,
            BatteryCapacityKwh = capacityKwh,
            InitialStateOfChargePct = initialSocPct,
            RequiredStateOfChargePct = requiredSocPct,
            MaxAcChargePowerKw = acLimitKw,
            MaxDcChargePowerKw = dcLimitKw,
            IsPriorityRoute = priority,
            ScheduledDeparture = At(1, departureHour, departureMinute),
            RosteredChargePointId = rosteredChargePointId
        };
}
