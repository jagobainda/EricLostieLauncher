using LostieLauncher.Utils;

namespace LostieLauncher.Tests.Utils;

public class VersionUtilsTests
{
    [Theory]
    [InlineData("1.2.0", "1.1.0", true)]
    [InlineData("1.1.0", "1.2.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    public void IsNewerVersion_ComparesNumericallyWhenBothParse(string remote, string local, bool expected)
    {
        // Arrange & Act
        var result = VersionUtils.IsNewerVersion(remote, local);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("v2.0.0", "v1.0.0", true)]
    [InlineData("V2.0.0", "1.0.0", true)]
    public void IsNewerVersion_StripsLeadingVPrefixBeforeComparing(string remote, string local, bool expected)
    {
        // Arrange & Act
        var result = VersionUtils.IsNewerVersion(remote, local);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void IsNewerVersion_StripsPreReleaseSuffixBeforeComparing()
    {
        // Arrange — base version 1.2.0 must beat 1.1.0 even with -beta suffix.
        var remote = "1.2.0-beta";
        var local = "1.1.0";

        // Act
        var result = VersionUtils.IsNewerVersion(remote, local);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsNewerVersion_WhenBaseVersionsEqualButPreReleaseSuffixDiffers_ComparesByBaseAndReturnsFalse()
    {
        // Arrange — Both parse to 1.0.0; suffix is ignored => not newer.
        var remote = "1.0.0-beta";
        var local = "1.0.0";

        // Act
        var result = VersionUtils.IsNewerVersion(remote, local);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("alpha", "beta")]
    [InlineData("beta", "alpha")]
    [InlineData("alpha", "alpha")]
    public void IsNewerVersion_WhenEitherInputIsUnparsable_FailsClosedAndReturnsFalse(string remote, string local)
    {
        // Arrange — Fail-closed: si una versión no es comparable numéricamente no se marca
        // actualización, evitando falsos positivos y downgrades automáticos vía AutoUpdate.
        // Act
        var result = VersionUtils.IsNewerVersion(remote, local);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsNewerVersion_WhenBaseVersionsParseButCaseDiffersInSuffix_ComparesByBaseAndReturnsFalse()
    {
        // Arrange — El ejemplo del HOW_TO_EXPLOIT ("v1.0-beta" vs "v1.0-BETA") en realidad sí
        // parsea: ParseBaseVersion recorta el sufijo y ambos quedan en base 1.0, por lo que la
        // comparación numérica ya devuelve false sin tocar la rama fail-closed.
        var remote = "v1.0-BETA";
        var local = "v1.0-beta";

        // Act
        var result = VersionUtils.IsNewerVersion(remote, local);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("1.0.0", "garbage")]
    [InlineData("garbage", "1.0.0")]
    public void IsNewerVersion_WhenOnlyOneInputIsUnparsable_FailsClosedAndReturnsFalse(string remote, string local)
    {
        // Arrange & Act
        var result = VersionUtils.IsNewerVersion(remote, local);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("1.0.0", "")]
    [InlineData("", "1.0.0")]
    public void IsNewerVersion_WhenEitherInputIsEmpty_FailsClosedAndReturnsFalse(string remote, string local)
    {
        // Arrange — Version por defecto es string.Empty; nunca debe marcarse update sobre datos vacíos.
        // Act
        var result = VersionUtils.IsNewerVersion(remote, local);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsNewerVersion_WhenInputIsNull_FailsClosedAndReturnsFalse()
    {
        // Arrange — System.Text.Json puede bindear un "version": null explícito pese al tipo no-anulable.
        // Act
        var result = VersionUtils.IsNewerVersion(null!, "1.0.0");

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("v4.0.3")]
    [InlineData("V4.0.3")]
    [InlineData("4.0.3")]
    public void FormatDisplayVersion_EmitsExactlyOneVPrefixRegardlessOfSourcePrefix(string version)
    {
        // Arrange & Act — el catálogo remoto no normaliza el prefijo, así que las tres formas llegan.
        var result = VersionUtils.FormatDisplayVersion(version);

        // Assert
        result.ShouldBe("v4.0.3");
    }

    [Fact]
    public void FormatDisplayVersion_WhenSourceAlreadyCarriesPrefix_DoesNotDuplicateIt()
    {
        // Arrange — Regresión del issue #29: los logs imprimían "vv2.18.0" al prefijar a mano una
        // versión de catálogo que ya traía la "v".
        var version = "v2.18.0";

        // Act
        var result = VersionUtils.FormatDisplayVersion(version);

        // Assert
        result.ShouldBe("v2.18.0");
        result.ShouldNotStartWith("vv");
    }

    [Fact]
    public void FormatDisplayVersion_CollapsesAnAlreadyDuplicatedPrefix()
    {
        // Arrange — Defensivo: si el contenido remoto llegara a publicar "vv2.18.0" el log no debe
        // arrastrar la duplicación.
        var version = "vv2.18.0";

        // Act
        var result = VersionUtils.FormatDisplayVersion(version);

        // Assert
        result.ShouldBe("v2.18.0");
    }

    [Fact]
    public void FormatDisplayVersion_PreservesPreReleaseSuffix()
    {
        // Arrange — A diferencia de ParseBaseVersion, el formateo es para mostrar: no recorta el sufijo.
        var version = "v1.2.0-beta";

        // Act
        var result = VersionUtils.FormatDisplayVersion(version);

        // Assert
        result.ShouldBe("v1.2.0-beta");
    }

    [Fact]
    public void FormatDisplayVersion_TrimsSurroundingWhitespace()
    {
        // Arrange — La config de versión especial es texto plano parseado a mano (clave=valor).
        var version = "  v2.11.0  ";

        // Act
        var result = VersionUtils.FormatDisplayVersion(version);

        // Assert
        result.ShouldBe("v2.11.0");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v")]
    public void FormatDisplayVersion_WhenThereIsNoVersionToShow_ReturnsUnknownInsteadOfALoneV(string? version)
    {
        // Arrange & Act — sin este caso el log quedaría como "Downloading: <game> v." sin información.
        var result = VersionUtils.FormatDisplayVersion(version);

        // Assert
        result.ShouldBe("unknown");
    }
}
