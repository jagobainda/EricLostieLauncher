using LostieLauncher.Utils;

namespace LostieLauncher.Tests.Utils;

public class SearchMatcherTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindMatches_WithEmptyOrWhitespaceTerm_ReturnsEmpty(string? term)
    {
        var matches = SearchMatcher.FindMatches("¿Cómo descargo un juego?", term);

        matches.ShouldBeEmpty();
    }

    [Fact]
    public void FindMatches_WithEmptyText_ReturnsEmpty()
    {
        var matches = SearchMatcher.FindMatches(null, "juego");

        matches.ShouldBeEmpty();
    }

    [Fact]
    public void FindMatches_IsCaseInsensitive()
    {
        var matches = SearchMatcher.FindMatches("Descargar el JUEGO", "juego");

        matches.ShouldBe([(13, 5)]);
    }

    [Fact]
    public void FindMatches_IsAccentInsensitive()
    {
        var matches = SearchMatcher.FindMatches("instalación en español", "instalacion");

        matches.Count.ShouldBe(1);
        matches[0].Start.ShouldBe(0);
        matches[0].Length.ShouldBe("instalación".Length);
    }

    [Fact]
    public void FindMatches_ReturnsAllOccurrences()
    {
        var matches = SearchMatcher.FindMatches("juego tras juego", "juego");

        matches.ShouldBe([(0, 5), (11, 5)]);
    }

    [Fact]
    public void Contains_WithMatch_ReturnsTrue() => SearchMatcher.Contains("¿Dónde se instalan los juegos?", "donde").ShouldBeTrue();

    [Fact]
    public void Contains_WithoutMatch_ReturnsFalse() => SearchMatcher.Contains("¿Dónde se instalan los juegos?", "biblioteca").ShouldBeFalse();
}
