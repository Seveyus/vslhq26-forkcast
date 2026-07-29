using System.Globalization;
using System.Text.RegularExpressions;

namespace Forkcast.Core.Ai;

/// <summary>
/// Small, dependency-free reader for the operational facts in an incident report.
/// </summary>
/// <remarks>
/// This is what makes the demonstration work with no Azure credentials at all. It is not as
/// good as a language model at reading unusual phrasing, and it is not meant to be: it exists
/// so the product degrades to something honest rather than to an error page.
/// </remarks>
public static partial class TextFacts
{
    private static readonly Dictionary<string, double> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10, ["eleven"] = 11,
        ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15,
        ["sixteen"] = 16, ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19,
        ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50
    };

    /// <summary>Finds a count that appears immediately before one of the given nouns.</summary>
    public static int? CountBefore(string text, params string[] nouns)
    {
        foreach (var noun in nouns)
        {
            var pattern = new Regex(
                $@"(?<n>\d+|[a-z]+)\s+(?:\w+\s+){{0,3}}?{Regex.Escape(noun)}",
                RegexOptions.IgnoreCase);

            foreach (Match match in pattern.Matches(text))
            {
                if (TryParseCount(match.Groups["n"].Value, out var value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    public static bool TryParseCount(string token, out int value)
    {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return value is >= 0 and <= 10_000;
        }

        if (NumberWords.TryGetValue(token, out var word))
        {
            value = (int)word;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>Returns every "HH:mm" in the text, in order of appearance.</summary>
    public static IReadOnlyList<string> ClockTimes(string text) =>
        ClockPattern().Matches(text).Select(m => m.Value).ToList();

    /// <summary>Reads a "from X% to Y%" battery range.</summary>
    public static (double Min, double Max)? PercentRange(string text)
    {
        var match = PercentRangePattern().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var low = double.Parse(match.Groups["low"].Value, CultureInfo.InvariantCulture);
        var high = double.Parse(match.Groups["high"].Value, CultureInfo.InvariantCulture);
        return low <= high ? (low, high) : (high, low);
    }

    [GeneratedRegex(@"\b([01]?\d|2[0-3]):[0-5]\d\b")]
    private static partial Regex ClockPattern();

    [GeneratedRegex(@"(?<low>\d{1,3})\s*%?\s*(?:to|-|–|and)\s*(?<high>\d{1,3})\s*%", RegexOptions.IgnoreCase)]
    private static partial Regex PercentRangePattern();
}
