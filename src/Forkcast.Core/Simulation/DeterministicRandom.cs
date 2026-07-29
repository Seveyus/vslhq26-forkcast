namespace Forkcast.Core.Simulation;

/// <summary>
/// Self-contained SplitMix64 pseudo random number generator.
/// </summary>
/// <remarks>
/// Forkcast deliberately avoids <see cref="System.Random"/>. A Forkcast result is only
/// trustworthy if a third party can regenerate the exact same numbers from the seed alone,
/// so the generator has to be part of the audited source rather than a runtime detail that
/// may change between .NET versions or platforms.
/// </remarks>
public sealed class DeterministicRandom
{
    private const double UnitScale = 1.0 / 9007199254740992.0; // 2^-53
    private const ulong Golden = 0x9E3779B97F4A7C15UL;

    private ulong _state;
    private double? _spareGaussian;

    public DeterministicRandom(long seed)
    {
        _state = unchecked((ulong)seed + Golden);
    }

    public ulong NextUInt64()
    {
        unchecked
        {
            _state += Golden;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * UnitScale;

    /// <summary>Uniform double in [min, max).</summary>
    public double NextDouble(double min, double max) => min + ((max - min) * NextDouble());

    public bool NextBool(double probability) => NextDouble() < probability;

    /// <summary>
    /// Standard Box-Muller transform. Consumes exactly two uniforms per pair of draws so the
    /// number of underlying generator steps stays predictable.
    /// </summary>
    public double NextGaussian(double mean = 0.0, double stdDev = 1.0)
    {
        if (_spareGaussian is { } spare)
        {
            _spareGaussian = null;
            return mean + (stdDev * spare);
        }

        // 1 - NextDouble() keeps the argument of Log strictly positive.
        var u1 = 1.0 - NextDouble();
        var u2 = NextDouble();
        var magnitude = Math.Sqrt(-2.0 * Math.Log(u1));
        var angle = 2.0 * Math.PI * u2;

        _spareGaussian = magnitude * Math.Sin(angle);
        return mean + (stdDev * magnitude * Math.Cos(angle));
    }

    public double NextGaussian(double mean, double stdDev, double min, double max)
        => Math.Clamp(NextGaussian(mean, stdDev), min, max);

    public double NextExponential(double mean)
    {
        var u = 1.0 - NextDouble();
        return -mean * Math.Log(u);
    }

    /// <summary>
    /// Derives a stable child seed. Uses FNV-1a rather than <see cref="string.GetHashCode()"/>,
    /// which is randomised per process and would break reproducibility.
    /// </summary>
    public static long DeriveSeed(long baseSeed, string stream, int index = 0)
    {
        unchecked
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            var hash = offsetBasis;
            hash = MixBytes(hash, BitConverter.GetBytes(baseSeed));
            foreach (var c in stream)
            {
                hash = (hash ^ (byte)(c & 0xFF)) * prime;
                hash = (hash ^ (byte)(c >> 8)) * prime;
            }

            hash = MixBytes(hash, BitConverter.GetBytes(index));
            return (long)hash;

            static ulong MixBytes(ulong hash, byte[] bytes)
            {
                foreach (var b in bytes)
                {
                    hash = (hash ^ b) * prime;
                }

                return hash;
            }
        }
    }
}
