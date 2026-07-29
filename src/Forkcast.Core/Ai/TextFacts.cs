using System.Globalization;
using System.Text.RegularExpressions;

namespace Forkcast.Core.Ai;

/// <summary>What to count, and how to tell the right occurrence from the wrong one.</summary>
public sealed record CountQuery
{
    /// <summary>Nouns to look for, in order of preference.</summary>
    public required IReadOnlyList<string> Nouns { get; init; }

    /// <summary>Words that mark a sentence as the one that states the count we want.</summary>
    public IReadOnlyList<string> Prefer { get; init; } = [];

    /// <summary>Words that mark a sentence as stating some other count.</summary>
    public IReadOnlyList<string> Avoid { get; init; } = [];

    /// <summary>When true, only a sentence matching <see cref="Prefer"/> may supply the count.</summary>
    public bool RequirePreferred { get; init; }
}

/// <summary>
/// Small, dependency-free reader for the operational facts in an incident report.
/// </summary>
/// <remarks>
/// This is what makes the demonstration work with no Azure credentials at all. It is not as good
/// as a language model at reading unusual phrasing and is not meant to be: it exists so the
/// product degrades to something honest rather than to an error page. Where it cannot tell two
/// readings apart it reports that it could not find the fact, rather than guessing.
/// </remarks>
public static partial class TextFacts
{
    /// <summary>How many words before the noun are searched for its count.</summary>
    private const int LookBehindWords = 6;

    private static readonly char[] WordSeparators = [' ', '\t', '\n', '\r', ',', ';', '(', ')'];

    private static readonly char[] SentenceTerminators = ['.', '!', '?', '\n'];

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10, ["eleven"] = 11,
        ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15,
        ["sixteen"] = 16, ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19,
        ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50
    };

    /// <summary>Finds the nearest count stated before one of the nouns.</summary>
    public static int? CountBefore(string text, params string[] nouns) =>
        Count(text, new CountQuery { Nouns = nouns });

    /// <summary>
    /// Finds a count, preferring the sentence that reads like the one being asked about.
    /// </summary>
    /// <remarks>
    /// The scoring exists because a single report routinely states two counts for the same noun:
    /// "two chargers failed. Ten charge points remain." Scanning for the first number before the
    /// first matching noun reads the wrong one. Sentences carrying a preferred word win,
    /// sentences carrying an avoided word lose, and everything else sits in between.
    /// </remarks>
    public static int? Count(string text, CountQuery query)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(query);

        var best = (Score: int.MaxValue, Value: (int?)null);

        foreach (var noun in query.Nouns)
        {
            var pattern = new Regex($@"\b{Regex.Escape(noun)}\b", RegexOptions.IgnoreCase);

            foreach (Match match in pattern.Matches(text))
            {
                var (start, end) = SentenceAround(text, match.Index);
                var sentence = text[start..end];

                var preferred = query.Prefer.Any(w => Contains(sentence, w));
                var avoided = query.Avoid.Any(w => Contains(sentence, w));

                if (query.RequirePreferred && !preferred)
                {
                    continue;
                }

                var score = preferred ? 0 : avoided ? 2 : 1;
                if (score >= best.Score)
                {
                    continue;
                }

                if (ScanBackwards(text, start, match.Index) is { } value)
                {
                    best = (score, value);
                    if (score == 0)
                    {
                        return value;
                    }
                }
            }
        }

        return best.Value;
    }

    /// <summary>Walks back from the noun, inside its own sentence, to the nearest number word.</summary>
    private static int? ScanBackwards(string text, int sentenceStart, int nounIndex)
    {
        var preceding = text[sentenceStart..nounIndex]
            .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(LookBehindWords)
            .Reverse();

        foreach (var word in preceding)
        {
            if (TryParseCount(word.Trim('.', ':', '-'), out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static (int Start, int End) SentenceAround(string text, int index)
    {
        var searchFrom = Math.Max(0, index - 1);
        var start = text.LastIndexOfAny(SentenceTerminators, searchFrom);
        start = start < 0 ? 0 : start + 1;

        var end = text.IndexOfAny(SentenceTerminators, index);
        end = end < 0 ? text.Length : end;

        return (start, end);
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    public static bool TryParseCount(string token, out int value)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return value is >= 0 and <= 10_000;
        }

        if (NumberWords.TryGetValue(token, out var word))
        {
            value = word;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>Returns every "HH:mm" in the text, in order of appearance.</summary>
    public static IReadOnlyList<string> ClockTimes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ClockPattern().Matches(text).Select(m => m.Value).ToList();
    }

    /// <summary>Reads a "from X% to Y%" battery range.</summary>
    public static (double Min, double Max)? PercentRange(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

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
