using System;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The GameAdapter status/moodle static content provider validation surface.
/// The test project never compile-references GameAdapter (it binds game
/// assemblies), so these lock the provider contracts reflectively: both kinds
/// are accepted as typed static descriptors, while a definition of another type
/// filed under a provider's kind, or invalid schema/scope data, is refused
/// before any future runtime domain consumes it.
/// </summary>
[Trait("Category", "Integration")]
public class StatusMoodleContentProviderTests
{
	private static object CreateProvider(string typeName)
	{
		var providerType = GameAssemblyHost.Adapter.GetType(typeName, throwOnError: true)!;
		var loggerType = typeof(NullLogger<>).MakeGenericType(providerType);
		var logger = loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
			?? throw new InvalidOperationException("NullLogger.Instance not found.");
		return Activator.CreateInstance(providerType, [logger])!;
	}

	private static bool TryBind(object provider, IModContentDefinition definition)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		var registration = new ModContentRegistration("mod.a", definition);
		return (bool)bind.Invoke(provider, [registration])!;
	}

	/// <summary>
	/// The provider's own lookup, so a refusal is asserted on the registry and
	/// not only on the return value: a definition the provider refused must not
	/// be kept.
	/// </summary>
	private static bool IsKept(object provider, string id)
	{
		var method = provider.GetType().GetMethod(
			"TryGetDefinition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryGetDefinition not found.");
		return (bool)method.Invoke(provider, [id, null])!;
	}

	[Fact]
	public void StatusProvider_AcceptsBodyAndLimbScopes()
	{
		var provider = CreateProvider(
			"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterStatusContentProvider");

		var body = new ModStatusDefinition { Id = "status.body", Scope = ModStatusScope.Body };
		var limb = new ModStatusDefinition { Id = "status.limb", Scope = ModStatusScope.Limb };

		Assert.True(TryBind(provider, body));
		Assert.True(TryBind(provider, limb));
	}

	[Fact]
	public void StatusProvider_RefusesADefinitionOfAnotherTypeFiledUnderItsKind()
	{
		var provider = CreateProvider(
			"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterStatusContentProvider");

		// The typed registry decodes no payload any more, so the reachable
		// refusal is a definition that claims the status kind without being a
		// ModStatusDefinition: it must be refused and never kept.
		Assert.False(TryBind(provider, new StubContentDefinition("status.bad", ModContentKind.Status)));
		Assert.False(IsKept(provider, "status.bad"));
	}

	[Fact]
	public void StatusProvider_ValidatesPerLimbMoodleRouting()
	{
		var provider = CreateProvider(
			"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterStatusContentProvider");

		var valid = new ModStatusDefinition
		{
			Id = "status.perlimb",
			Scope = ModStatusScope.Limb,
			MoodleId = "moodle.default",
			ShowPerLimbMoodles = true,
			LimbMoodles =
			[
				new ModLimbMoodleBinding { LimbName = "LeftArm", MoodleId = "moodle.left" }
			]
		};

		var bodyWithPerLimb = new ModStatusDefinition
		{
			Id = "status.body-perlimb",
			Scope = ModStatusScope.Body,
			MoodleId = "moodle.default",
			ShowPerLimbMoodles = true
		};

		var bindingsWithoutFlag = new ModStatusDefinition
		{
			Id = "status.bindings-no-flag",
			Scope = ModStatusScope.Limb,
			MoodleId = "moodle.default",
			LimbMoodles =
			[
				new ModLimbMoodleBinding { LimbName = "LeftArm", MoodleId = "moodle.left" }
			]
		};

		var duplicateLimb = new ModStatusDefinition
		{
			Id = "status.duplicate-limb",
			Scope = ModStatusScope.Limb,
			MoodleId = "moodle.default",
			ShowPerLimbMoodles = true,
			LimbMoodles =
			[
				new ModLimbMoodleBinding { LimbName = "LeftArm", MoodleId = "moodle.left" },
				new ModLimbMoodleBinding { LimbName = "leftarm", MoodleId = "moodle.left-dup" }
			]
		};

		Assert.True(TryBind(provider, valid));
		Assert.False(TryBind(provider, bodyWithPerLimb));
		Assert.False(TryBind(provider, bindingsWithoutFlag));
		Assert.False(TryBind(provider, duplicateLimb));
	}

	[Fact]
	public void MoodleProvider_RequiresIconAndRejectsInvalidNumericFields()
	{
		var provider = CreateProvider(
			"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterMoodleContentProvider");

		var valid = new ModMoodleDefinition { Id = "moodle.valid", IconId = "icons.lead", Intensity = 2, HoldSeconds = 1f };
		var validAnimated = new ModMoodleDefinition
		{
			Id = "moodle.valid-animated",
			IconId = "icons.lead",
			Intensity = 2,
			HoldSeconds = 1f,
			IconAnimation = new ModMoodleAnimation
			{
				FramePaths = ["Fx/Moodle0", "Fx/Moodle1"],
				FramesPerSecond = 10f,
				Loop = true
			}
		};
		var missingIcon = new ModMoodleDefinition { Id = "moodle.no-icon", IconId = "" };
		var negativeHold = new ModMoodleDefinition { Id = "moodle.neg-hold", IconId = "icons.lead", HoldSeconds = -1f };
		var negativeIntensity = new ModMoodleDefinition { Id = "moodle.neg-int", IconId = "icons.lead", Intensity = -1 };
		var invalidAnimationFps = new ModMoodleDefinition
		{
			Id = "moodle.bad-anim-fps",
			IconId = "icons.lead",
			IconAnimation = new ModMoodleAnimation
			{
				FramePaths = ["Fx/Moodle0"],
				FramesPerSecond = float.NaN
			}
		};
		var invalidAnimationEmpty = new ModMoodleDefinition
		{
			Id = "moodle.bad-anim-empty",
			IconId = "icons.lead",
			IconAnimation = new ModMoodleAnimation
			{
				FramePaths = [],
				FramesPerSecond = 12f
			}
		};

		Assert.True(TryBind(provider, valid));
		Assert.True(TryBind(provider, validAnimated));
		Assert.False(TryBind(provider, missingIcon));
		Assert.False(TryBind(provider, negativeHold));
		Assert.False(TryBind(provider, negativeIntensity));
		Assert.False(TryBind(provider, invalidAnimationFps));
		Assert.False(TryBind(provider, invalidAnimationEmpty));
	}

	[Fact]
	public void MoodleProvider_RejectsOverlongLimbDisplayFormats()
	{
		var provider = CreateProvider(
			"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterMoodleContentProvider");

		var tooLong = new ModMoodleDefinition
		{
			Id = "moodle.long-limb-format",
			IconId = "icons.lead",
			LimbDisplayNameFormat = new string('x', 257)
		};

		Assert.False(TryBind(provider, tooLong));
	}
}
