using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Diagnostics;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Diagnostics;

/// <summary>
/// The build stamp's two FORMS, which are the whole of what this type decides: what a log shows (the stamp
/// the build recorded, character for character) and what a screen shows (the same stamp with a short
/// revision, and with any marker the revision carried kept). Every case here is a shape a real build can
/// carry, including the ones that make the short form meaningless — a version with no revision at all, and a
/// stamp whose revision never arrived.
/// </summary>
public class BuildIdentityTests
{
	[Fact]
	public void AStampWithARevision_KeepsTheNumericVersionAndShortensTheRevision() =>
		Assert.Equal("0.1.0+abc1234", BuildIdentity.Short("0.1.0+abc1234567890abcdef"));

	[Fact]
	public void ADirtyStamp_KeepsTheMarkerThatDistinguishesItFromACleanBuild() =>
		Assert.Equal("0.1.0+abc1234-dirty", BuildIdentity.Short("0.1.0+abc1234567890abcdef-dirty"));

	[Fact]
	public void ARevisionAlreadyShort_IsShownWhole() =>
		Assert.Equal("0.1.0+abc", BuildIdentity.Short("0.1.0+abc"));

	[Fact]
	public void AStampWithoutARevision_IsShownUnchangedRatherThanTrimmed() =>
		Assert.Equal("0.1.0", BuildIdentity.Short("0.1.0"));

	[Fact]
	public void AnEmptyStamp_SaysUnknownRatherThanShowingNothing() =>
		Assert.Equal("unknown", BuildIdentity.Short(string.Empty));

	[Fact]
	public void ANullStamp_SaysUnknownRatherThanThrowing() =>
		Assert.Equal("unknown", BuildIdentity.Short(null!));

	[Fact]
	public void ATrailingPlus_IsShownUnchanged() =>
		Assert.Equal("0.1.0+", BuildIdentity.Short("0.1.0+"));

	[Fact]
	public void Of_ReadsTheAttributeTheBuildWroteRatherThanComposingOne()
	{
		var assembly = typeof(BuildIdentityTests).Assembly;
		var written = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

		Assert.NotNull(written);
		Assert.Equal(written, BuildIdentity.Of(assembly));
	}

	[Fact]
	public void Of_RejectsANullAssemblyRatherThanReturningUnknownForIt() =>
		Assert.Throws<ArgumentNullException>(() => BuildIdentity.Of(null!));
}
