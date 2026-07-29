using Forkcast.Core.Ai;
using Forkcast.Core.Simulation;

namespace Forkcast.Core.Incidents;

/// <summary>
/// Turns a loosely structured <see cref="IncidentDraft"/> into a fully specified
/// <see cref="Incident"/> the simulation can run, using a site template for everything the
/// draft does not state.
/// </summary>
/// <remarks>
/// This is the guard between the language boundary and the engine. Whatever a model reports,
/// what reaches the simulation is a clamped, internally consistent depot: a fleet that exists,
/// connectors that exist, and a deadline after the incident. Every substitution or clamp is
/// reported so the user can see what was assumed on their behalf.
/// </remarks>
public sealed class IncidentComposer
{
    private const int MinFleet = 4;
    private const int MaxFleet = 60;
    private const int MaxChargePoints = 40;

    private static readonly double[] CapacityCycle = [110, 140, 110, 140, 110, 90, 90, 110, 90, 140];
    private static readonly double[] RequiredSocCycle = [88, 90, 86, 92, 84, 88, 80, 82, 78, 90];

    public (Incident Incident, IReadOnlyList<DraftAdjustment> Adjustments) Compose(
        IncidentDraft draft,
        Incident template,
        string narrative)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(template);

        var adjustments = new List<DraftAdjustment>();

        var fleetSize = Clamp(
            draft.VehicleCount ?? template.VehicleCount, MinFleet, MaxFleet, "vehicleCount", adjustments);
        var operational = Clamp(
            draft.OperationalChargePointCount ?? template.OperationalChargePointCount,
            1, MaxChargePoints, "operationalChargePointCount", adjustments);
        var failed = Clamp(
            draft.FailedChargePointCount ?? template.FailedChargePointCount,
            0, MaxChargePoints, "failedChargePointCount", adjustments);
        var priority = Clamp(
            draft.PriorityVehicleCount ?? template.PriorityVehicleCount,
            0, fleetSize, "priorityVehicleCount", adjustments);

        var minSoc = draft.MinInitialStateOfChargePct ?? MinOf(template, v => v.InitialStateOfChargePct);
        var maxSoc = draft.MaxInitialStateOfChargePct ?? MaxOf(template, v => v.InitialStateOfChargePct);
        if (minSoc > maxSoc)
        {
            (minSoc, maxSoc) = (maxSoc, minSoc);
            adjustments.Add(new DraftAdjustment
            {
                Field = "initialStateOfCharge",
                Reason = "The battery range was given with the higher value first, so it was swapped."
            });
        }

        minSoc = Math.Clamp(minSoc, 1, 95);
        maxSoc = Math.Clamp(maxSoc, minSoc + 1, 99);

        var detectedAt = ApplyClockTime(template.DetectedAt, draft.DetectedAtLocalTime)
                         ?? template.DetectedAt;
        var deadline = ApplyClockTime(detectedAt, draft.DeadlineLocalTime) ?? template.DepartureDeadline;
        if (deadline <= detectedAt)
        {
            deadline = deadline.AddDays(1);
        }

        if ((deadline - detectedAt).TotalHours > 36)
        {
            deadline = detectedAt.AddHours(36);
            adjustments.Add(new DraftAdjustment
            {
                Field = "departureDeadline",
                Reason = "The deadline was more than 36 hours out, so it was capped."
            });
        }

        var matchesTemplate =
            fleetSize == template.VehicleCount
            && operational == template.OperationalChargePointCount
            && failed == template.FailedChargePointCount
            && priority == template.PriorityVehicleCount
            && Math.Abs(minSoc - MinOf(template, v => v.InitialStateOfChargePct)) < 0.01
            && Math.Abs(maxSoc - MaxOf(template, v => v.InitialStateOfChargePct)) < 0.01
            && detectedAt == template.DetectedAt
            && deadline == template.DepartureDeadline;

        if (matchesTemplate)
        {
            // Nothing meaningful changed, so keep the published scenario byte for byte and the
            // published numbers with it.
            return (template with
            {
                Narrative = string.IsNullOrWhiteSpace(narrative) ? template.Narrative : narrative
            }, adjustments);
        }

        var chargePoints = BuildChargePoints(template, operational, failed);
        var fleet = BuildFleet(template, chargePoints, fleetSize, priority, minSoc, maxSoc, deadline);

        var incident = template with
        {
            Id = $"INC-{detectedAt:yyyyMMdd}-CUSTOM",
            Title = string.IsNullOrWhiteSpace(draft.Title) ? template.Title : draft.Title!,
            Site = string.IsNullOrWhiteSpace(draft.Site) ? template.Site : draft.Site!,
            Narrative = string.IsNullOrWhiteSpace(narrative) ? template.Narrative : narrative,
            DetectedAt = detectedAt,
            DepartureDeadline = deadline,
            Fleet = fleet,
            ChargePoints = chargePoints,
            Tariff = ShiftTariff(template, detectedAt, deadline),
            Constraints = template.Constraints with
            {
                FaultRecoveryWindowStart = detectedAt.AddHours((deadline - detectedAt).TotalHours * 0.4),
                FaultRecoveryWindowEnd = detectedAt.AddHours((deadline - detectedAt).TotalHours * 0.8)
            },
            Failures = draft.Failures.Count > 0 ? draft.Failures : template.Failures
        };

        return (incident, adjustments);
    }

    private static IReadOnlyList<ChargePoint> BuildChargePoints(
        Incident template,
        int operational,
        int failed)
    {
        var acRating = template.ChargePoints
            .Where(c => c.Kind == ChargePointKind.DepotAc)
            .Select(c => c.RatedPowerKw)
            .DefaultIfEmpty(11.0)
            .First();

        var dcRating = template.ChargePoints
            .Where(c => c.Kind == ChargePointKind.DcFast)
            .Select(c => c.RatedPowerKw)
            .DefaultIfEmpty(150.0)
            .First();

        var points = new List<ChargePoint>(operational + failed);
        for (var i = 1; i <= operational; i++)
        {
            points.Add(new ChargePoint
            {
                Id = $"CP-{i:00}",
                Kind = ChargePointKind.DepotAc,
                RatedPowerKw = acRating,
                IsOperational = true
            });
        }

        for (var i = 1; i <= failed; i++)
        {
            points.Add(new ChargePoint
            {
                Id = $"CP-{operational + i:00}",
                Kind = ChargePointKind.DcFast,
                RatedPowerKw = dcRating,
                IsOperational = false,
                FaultCode = "E-4412",
                FaultSummary = "Reported offline in the incident report"
            });
        }

        return points;
    }

    private static IReadOnlyList<Vehicle> BuildFleet(
        Incident template,
        IReadOnlyList<ChargePoint> chargePoints,
        int fleetSize,
        int priorityCount,
        double minSoc,
        double maxSoc,
        DateTimeOffset deadline)
    {
        // Seeded from the shape of the depot, so the same description always yields the same
        // fleet without anyone having to store it.
        var random = new DeterministicRandom(
            DeterministicRandom.DeriveSeed(SimulationOptions.DefaultSeed, "fleet", fleetSize * 131 + priorityCount));

        var departureWindow = TimeSpan.FromMinutes(80);
        var step = fleetSize > 1 ? departureWindow.TotalMinutes / (fleetSize - 1) : 0.0;

        var fleet = new List<Vehicle>(fleetSize);
        for (var i = 0; i < fleetSize; i++)
        {
            var capacity = CapacityCycle[i % CapacityCycle.Length];
            var isPriority = i < priorityCount;

            // Spread evenly across the stated range, then jitter within the gap so the fleet
            // does not look artificially uniform.
            var position = fleetSize > 1 ? i / (double)(fleetSize - 1) : 0.5;
            var soc = minSoc + ((maxSoc - minSoc) * position);
            if (i is not 0 && i != fleetSize - 1)
            {
                soc += random.NextDouble(-2.0, 2.0);
            }

            fleet.Add(new Vehicle
            {
                Id = $"EV-{i + 1:00}",
                Route = isPriority ? $"Priority {i + 1}" : $"Standard {i + 1}",
                BatteryCapacityKwh = capacity,
                InitialStateOfChargePct = Math.Round(Math.Clamp(soc, minSoc, maxSoc), 0),
                RequiredStateOfChargePct = RequiredSocCycle[i % RequiredSocCycle.Length],
                MaxAcChargePowerKw = 11.0,
                MaxDcChargePowerKw = capacity switch { >= 140 => 90.0, >= 110 => 60.0, _ => 50.0 },
                IsPriorityRoute = isPriority,
                ScheduledDeparture = deadline.AddMinutes(-step * (fleetSize - 1 - i)),
                RosteredChargePointId = chargePoints[i % chargePoints.Count].Id
            });
        }

        return fleet;
    }

    /// <summary>Stretches the template tariff bands onto the new charging window.</summary>
    private static IReadOnlyList<TariffWindow> ShiftTariff(
        Incident template,
        DateTimeOffset detectedAt,
        DateTimeOffset deadline)
    {
        var templateSpan = (template.DepartureDeadline - template.DetectedAt).TotalMinutes;
        var newSpan = (deadline - detectedAt).TotalMinutes;
        if (templateSpan <= 0 || newSpan <= 0)
        {
            return template.Tariff;
        }

        var scale = newSpan / templateSpan;
        return template.Tariff
            .Select(w => w with
            {
                From = detectedAt.AddMinutes((w.From - template.DetectedAt).TotalMinutes * scale),
                To = detectedAt.AddMinutes((w.To - template.DetectedAt).TotalMinutes * scale)
            })
            .ToList();
    }

    private static DateTimeOffset? ApplyClockTime(DateTimeOffset reference, string? clock)
    {
        if (string.IsNullOrWhiteSpace(clock) || !TimeOnly.TryParse(clock, out var time))
        {
            return null;
        }

        var candidate = new DateTimeOffset(
            reference.Year, reference.Month, reference.Day,
            time.Hour, time.Minute, 0, reference.Offset);

        return candidate;
    }

    private static int Clamp(int value, int min, int max, string field, List<DraftAdjustment> adjustments)
    {
        var clamped = Math.Clamp(value, min, max);
        if (clamped != value)
        {
            adjustments.Add(new DraftAdjustment
            {
                Field = field,
                Reason = $"Value {value} was outside the supported range {min} to {max}, so it was clamped to {clamped}."
            });
        }

        return clamped;
    }

    private static double MinOf(Incident incident, Func<Vehicle, double> selector) =>
        incident.Fleet.Count == 0 ? 20 : incident.Fleet.Min(selector);

    private static double MaxOf(Incident incident, Func<Vehicle, double> selector) =>
        incident.Fleet.Count == 0 ? 80 : incident.Fleet.Max(selector);
}
