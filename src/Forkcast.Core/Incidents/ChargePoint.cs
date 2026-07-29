namespace Forkcast.Core.Incidents;

public enum ChargePointKind
{
    /// <summary>Depot AC wall box. Slow, plentiful, limited by the vehicle onboard charger.</summary>
    DepotAc,

    /// <summary>Site DC fast charger. High power, drawn from the grid connection.</summary>
    DcFast,

    /// <summary>Outlet on a towed battery unit. Battery fed, so it bypasses the grid limit.</summary>
    MobileBuffer
}

/// <summary>
/// A physical place a vehicle can be plugged in.
/// </summary>
public sealed record ChargePoint
{
    public required string Id { get; init; }

    public required ChargePointKind Kind { get; init; }

    public required double RatedPowerKw { get; init; }

    public required bool IsOperational { get; init; }

    /// <summary>Manufacturer fault code when the charge point is down. Null when healthy.</summary>
    public string? FaultCode { get; init; }

    /// <summary>Human readable fault summary shown in the incident card.</summary>
    public string? FaultSummary { get; init; }

    /// <summary>True when the charge point draws from the site grid connection.</summary>
    public bool DrawsFromGrid => Kind is ChargePointKind.DepotAc or ChargePointKind.DcFast;
}
