using Forkcast.Core.Simulation;

namespace Forkcast.Tests;

public class DeterministicRandomTests
{
    [Fact]
    public void Same_seed_produces_the_same_sequence()
    {
        var a = new DeterministicRandom(20260728);
        var b = new DeterministicRandom(20260728);

        for (var i = 0; i < 200; i++)
        {
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }
    }

    [Fact]
    public void Different_seeds_diverge()
    {
        var a = new DeterministicRandom(1);
        var b = new DeterministicRandom(2);

        var differences = 0;
        for (var i = 0; i < 100; i++)
        {
            if (a.NextUInt64() != b.NextUInt64())
            {
                differences++;
            }
        }

        Assert.Equal(100, differences);
    }

    [Fact]
    public void Uniform_draws_stay_in_the_unit_interval()
    {
        var random = new DeterministicRandom(7);

        for (var i = 0; i < 10_000; i++)
        {
            var value = random.NextDouble();
            Assert.InRange(value, 0.0, 0.9999999999);
        }
    }

    [Fact]
    public void Uniform_draws_have_the_expected_mean()
    {
        var random = new DeterministicRandom(11);
        var total = 0.0;

        for (var i = 0; i < 50_000; i++)
        {
            total += random.NextDouble();
        }

        Assert.InRange(total / 50_000, 0.49, 0.51);
    }

    [Fact]
    public void Gaussian_draws_match_the_requested_moments()
    {
        var random = new DeterministicRandom(13);
        var samples = new double[50_000];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = random.NextGaussian(10.0, 2.0);
        }

        var mean = samples.Average();
        var variance = samples.Sum(s => (s - mean) * (s - mean)) / samples.Length;

        Assert.InRange(mean, 9.9, 10.1);
        Assert.InRange(Math.Sqrt(variance), 1.95, 2.05);
    }

    [Fact]
    public void Clamped_gaussian_never_leaves_its_bounds()
    {
        var random = new DeterministicRandom(17);

        for (var i = 0; i < 20_000; i++)
        {
            Assert.InRange(random.NextGaussian(0.0, 5.0, -1.0, 1.0), -1.0, 1.0);
        }
    }

    [Fact]
    public void Exponential_draws_match_the_requested_mean()
    {
        var random = new DeterministicRandom(19);
        var total = 0.0;

        for (var i = 0; i < 50_000; i++)
        {
            total += random.NextExponential(12.0);
        }

        Assert.InRange(total / 50_000, 11.5, 12.5);
    }

    [Fact]
    public void Derived_seeds_are_stable_across_calls_and_distinct_per_stream()
    {
        var first = DeterministicRandom.DeriveSeed(20260728, "trial", 4);
        var again = DeterministicRandom.DeriveSeed(20260728, "trial", 4);
        var otherIndex = DeterministicRandom.DeriveSeed(20260728, "trial", 5);
        var otherStream = DeterministicRandom.DeriveSeed(20260728, "fleet", 4);

        Assert.Equal(first, again);
        Assert.NotEqual(first, otherIndex);
        Assert.NotEqual(first, otherStream);
    }

    [Fact]
    public void Derived_seeds_do_not_depend_on_runtime_string_hashing()
    {
        // string.GetHashCode is randomised per process, so a literal expectation here is the
        // regression test: if the derivation ever starts using it, this value will move.
        Assert.Equal(-7475381083268582224L, DeterministicRandom.DeriveSeed(20260728, "trial", 0));
    }
}
