using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The Phase 4b SemVer parser/comparer: strict SemVer 2.0.0 validation and
/// full precedence ordering. The handshake only needs precedence equality,
/// but the ordering is implemented so a future compatibility-range policy can
/// reuse the same judge without re-deriving the spec.
/// </summary>
public class SemanticVersionTests
{
	[Theory]
	[InlineData("1.2.3")]
	[InlineData("0.0.0")]
	[InlineData("10.20.30")]
	[InlineData("1.0.0-alpha")]
	[InlineData("1.0.0-alpha.1")]
	[InlineData("1.0.0-alpha.beta")]
	[InlineData("1.0.0-beta.11")]
	[InlineData("1.0.0-rc.1")]
	[InlineData("1.0.0+build.5")]
	[InlineData("1.0.0-alpha+build")]
	[InlineData("1.0.0+exp.sha.5114f85")]
	public void ValidVersions_Parse(string text)
	{
		Assert.True(SemanticVersion.TryParse(text, out var version), $"{text} must parse");
		Assert.Equal(text, version!.Raw);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("1")]
	[InlineData("1.2")]
	[InlineData("1.2.3.4")]
	[InlineData("01.2.3")]
	[InlineData("1.02.3")]
	[InlineData("1.2.03")]
	[InlineData("1.2.3-")]
	[InlineData("1.2.3+")]
	[InlineData("1.2.3-alpha..1")]
	[InlineData("1.2.3-alpha_1")]
	[InlineData("1.2.3-01")]
	[InlineData("1.2.3+build..5")]
	[InlineData("1.2.3+build_5")]
	[InlineData("v1.2.3")]
	[InlineData(" 1.2.3")]
	[InlineData("1.2.3 ")]
	public void InvalidVersions_DoNotParse(string text) =>
		Assert.False(SemanticVersion.TryParse(text, out _), $"{text} must not parse");

	[Fact]
	public void OverlongVersion_DoesNotParse() => Assert.False(SemanticVersion.TryParse(new string('1', SemanticVersion.MaxLength + 1), out _));

	[Fact]
	public void Precedence_FollowsTheSemVerOrder()
	{
		var ordered = new[]
		{
			"1.0.0-alpha",
			"1.0.0-alpha.1",
			"1.0.0-alpha.beta",
			"1.0.0-beta",
			"1.0.0-beta.2",
			"1.0.0-beta.11",
			"1.0.0-rc.1",
			"1.0.0",
		};

		for (var i = 0; i + 1 < ordered.Length; i++)
		{
			Assert.True(Parse(ordered[i]).CompareTo(Parse(ordered[i + 1])) < 0,
				$"{ordered[i]} must sort before {ordered[i + 1]}");
		}
	}

	[Fact]
	public void BuildMetadata_DoesNotAffectPrecedence()
	{
		Assert.True(Parse("1.0.0+host.1").PrecedenceEquals(Parse("1.0.0+guest.2")));
		Assert.Equal(0, Parse("1.0.0").CompareTo(Parse("1.0.0+build")));
	}

	[Fact]
	public void Prerelease_DiffersFromRelease()
	{
		Assert.False(Parse("1.0.0-alpha").PrecedenceEquals(Parse("1.0.0")));
		Assert.True(Parse("1.0.0-alpha").CompareTo(Parse("1.0.0")) < 0);
	}

	private static SemanticVersion Parse(string text)
	{
		Assert.True(SemanticVersion.TryParse(text, out var version), $"{text} must parse in this context");
		return version!;
	}
}
