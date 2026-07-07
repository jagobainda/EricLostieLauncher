using System.Globalization;

namespace LostieLauncher.Utils;

public static class SearchMatcher
{
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static bool Contains(string? text, string? term) => FindMatches(text, term).Count > 0;

    public static IReadOnlyList<(int Start, int Length)> FindMatches(string? text, string? term)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(term)) return [];

        var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
        var matches = new List<(int, int)>();
        var start = 0;

        while (start < text.Length)
        {
            var index = compareInfo.IndexOf(text.AsSpan(start), term.AsSpan(), Options, out var matchLength);
            if (index < 0 || matchLength <= 0) break;

            matches.Add((start + index, matchLength));
            start += index + matchLength;
        }

        return matches;
    }
}
