using LostieLauncher.Utils;

namespace LostieLauncher.Tests.Utils;

public class LinkTextParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_ReturnsEmpty_ForNullOrEmpty(string? text)
    {
        // Act & Assert
        LinkTextParser.Parse(text).ShouldBeEmpty();
    }

    [Fact]
    public void Parse_ReturnsSinglePlainSegment_WhenNoLinks()
    {
        // Act
        var segments = LinkTextParser.Parse("Texto normal sin enlaces.");

        // Assert
        var segment = segments.ShouldHaveSingleItem();
        segment.IsLink.ShouldBeFalse();
        segment.Text.ShouldBe("Texto normal sin enlaces.");
    }

    [Fact]
    public void Parse_DetectsHttpsUrl()
    {
        // Act
        var segments = LinkTextParser.Parse("Mira https://github.com/jagobainda/LostieLauncher para más info");

        // Assert
        segments.Count.ShouldBe(3);
        segments[0].Text.ShouldBe("Mira ");
        segments[1].IsLink.ShouldBeTrue();
        segments[1].Text.ShouldBe("https://github.com/jagobainda/LostieLauncher");
        segments[1].Url.ShouldBe("https://github.com/jagobainda/LostieLauncher");
        segments[2].Text.ShouldBe(" para más info");
    }

    [Fact]
    public void Parse_DetectsBareDomainWithPath_AndNormalizesToHttps()
    {
        // El caso real de la news de la v0.9.0: dominio sin esquema y con punto final de frase.
        var segments = LinkTextParser.Parse("Puedes consultar el changelog completo en github.com/jagobainda/LostieLauncher/releases.");

        // Assert
        segments.Count.ShouldBe(3);
        segments[1].IsLink.ShouldBeTrue();
        segments[1].Text.ShouldBe("github.com/jagobainda/LostieLauncher/releases");
        segments[1].Url.ShouldBe("https://github.com/jagobainda/LostieLauncher/releases");
        segments[2].Text.ShouldBe(".");
    }

    [Fact]
    public void Parse_DetectsWwwPrefixedDomain()
    {
        // Act
        var segments = LinkTextParser.Parse("Visita www.example.org ahora");

        // Assert
        segments[1].IsLink.ShouldBeTrue();
        segments[1].Url.ShouldBe("https://www.example.org/");
    }

    [Fact]
    public void Parse_TrimsTrailingPunctuation()
    {
        // Act
        var segments = LinkTextParser.Parse("(ver https://example.com/docs), ¿vale?");

        // Assert
        var link = segments.Single(s => s.IsLink);
        link.Text.ShouldBe("https://example.com/docs");
        segments.Last().Text.ShouldBe("), ¿vale?");
    }

    [Fact]
    public void Parse_DetectsMultipleLinks()
    {
        // Act
        var segments = LinkTextParser.Parse("Repo: github.com/a/b y web: https://example.com");

        // Assert
        segments.Count(s => s.IsLink).ShouldBe(2);
    }

    [Theory]
    [InlineData("Guarda el archivo.txt en la carpeta")]          // TLD no conocido: no es link
    [InlineData("Escríbenos a soporte@example.com si falla")]    // email: no es link
    [InlineData("La versión v0.9.0 ya está disponible")]         // números con puntos: no es link
    public void Parse_IgnoresNonLinks(string text)
    {
        // Act & Assert
        LinkTextParser.Parse(text).ShouldAllBe(s => !s.IsLink);
    }

    [Fact]
    public void Parse_IgnoresHttpUrls()
    {
        // Solo se permite HTTPS (misma política que UrlLauncher, BUG-063).
        var segments = LinkTextParser.Parse("Antiguo mirror en http://example.com/old sin soporte");

        // Assert
        segments.ShouldAllBe(s => !s.IsLink);
    }
}
