using System;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The GameAdapter status/moodle static content provider validation surface.
/// The test project never compile-references GameAdapter (it binds game
/// assemblies), so these lock the provider contracts reflectively: both kinds
/// are accepted as typed static descriptors, while invalid schema/scope data is
/// refused before any future runtime domain consumes it.
/// </summary>
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

	private static bool TryBind(object provider, string kind, string id, byte[] payload)
	{
		var bind = provider.GetType().GetMethod(
			"TryBind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("TryBind not found.");
		var registration = new ModContentRegistration(
			"mod.a",
			new ModContentDefinition(id, kind, payload, 1));
		return (bool)bind.Invoke(provider, [registration])!;
	}

	[Fact]
	public void StatusProvider_AcceptsBodyAndLimbScopes()
	{
		var provider = CreateProvider(
			"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterStatusContentProvider");

		var body = new ModStatusDefinition { Scope = ModStatusScope.Body };
		var limb = new ModStatusDefinition { Scope = ModStatusScope.Limb };

		Assert.True(TryBind(provider, ModContentKind.Status, "status.body", body.ToPayload()));
		Assert.True(TryBind(provider, ModContentKind.Status, "status.limb", limb.ToPayload()));
	}

	[Fact]
	public void StatusProvider_RejectsInvalidPayload()
	{
		var provider = CreateProvider(
			"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterStatusContentProvider");

		Assert.False(TryBind(provider, ModContentKind.Status, "status.bad", [1, 2, 3]));
	}

	[Fact]
	public void MoodleProvider_RequiresIconAndRejectsInvalidNumericFields()
	{
		var provider = CreateProvider(
			"CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterMoodleContentProvider");

		var valid = new ModMoodleDefinition { IconId = "icons.lead", Intensity = 2, HoldSeconds = 1f };
		var missingIcon = new ModMoodleDefinition { IconId = "" };
		var negativeHold = new ModMoodleDefinition { IconId = "icons.lead", HoldSeconds = -1f };
		var negativeIntensity = new ModMoodleDefinition { IconId = "icons.lead", Intensity = -1 };

		Assert.True(TryBind(provider, ModContentKind.Moodle, "moodle.valid", valid.ToPayload()));
		Assert.False(TryBind(provider, ModContentKind.Moodle, "moodle.no-icon", missingIcon.ToPayload()));
		Assert.False(TryBind(provider, ModContentKind.Moodle, "moodle.neg-hold", negativeHold.ToPayload()));
		Assert.False(TryBind(provider, ModContentKind.Moodle, "moodle.neg-int", negativeIntensity.ToPayload()));
	}
}
