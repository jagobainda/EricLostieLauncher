using System.Text.RegularExpressions;

namespace LostieLauncher.Utils;

public static partial class LinkTextParser
{
    public readonly record struct Segment(string Text, string? Url)
    {
        public bool IsLink => Url is not null;
    }

    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '!', '?', ')', ']', '"', '\''];

    public static IReadOnlyList<Segment> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var segments = new List<Segment>();
        var lastIndex = 0;

        foreach (Match match in LinkRegex().Matches(text))
        {
            var candidate = match.Value.TrimEnd(TrailingPunctuation);
            if (candidate.Length == 0) continue;

            var normalized = candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : $"https://{candidate}";

            if (!UrlLauncher.TryGetHttpsUri(normalized, out var uri)) continue;

            if (match.Index > lastIndex) segments.Add(new Segment(text[lastIndex..match.Index], null));

            segments.Add(new Segment(candidate, uri.AbsoluteUri));
            lastIndex = match.Index + candidate.Length;
        }

        if (lastIndex < text.Length) segments.Add(new Segment(text[lastIndex..], null));

        return segments;
    }

    [GeneratedRegex(
        @"https://\S+|(?<![\w@./-])(?:www\.[\w-]+(?:\.[\w-]+)+|(?:[\w-]+\.)+(?:com|net|org|es|eu|io|gg|dev|app|me|co|tv|info)\b)(?:/\S*)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();
}
