using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The generic content binder: it routes content registrations to
/// per-kind providers after mod discovery, and it only binds content from
/// network modes that guarantee all peers have the same static content.
/// </summary>
[Trait("Category", "Integration")]
public class ModContentBinderTests
{
	[Fact]
	public void BindsSharedContentToMatchingProvider()
	{
		var provider = new RecordingProvider(ModContentKind.Item);
		var binder = CreateBinder(
			new FakeContentControl(
				new ModContentRegistration("shared.mod", new StubContentDefinition("sword", ModContentKind.Item))),
			new FakeModsControl([
				new ModManifest("shared.mod", "Shared", "1.0.0", NetworkMode.Synchronized, null)
			]),
			provider);

		binder.Update();

		var bound = Assert.Single(provider.Bound);
		Assert.Equal("sword", bound.Definition.Id);
		Assert.Equal("shared.mod", bound.ModId);
	}

	[Fact]
	public void SkipsContentFromNonSharedMods()
	{
		var provider = new RecordingProvider(ModContentKind.Item);
		var binder = CreateBinder(
			new FakeContentControl(
				new ModContentRegistration("host.mod", new StubContentDefinition("sword", ModContentKind.Item)),
				new ModContentRegistration("shared.mod", new StubContentDefinition("tool", ModContentKind.Item))),
			new FakeModsControl([
				new ModManifest("host.mod", "Host", "1.0.0", NetworkMode.HostOnly, null),
				new ModManifest("shared.mod", "Shared", "1.0.0", NetworkMode.Authoritative, null)
			]),
			provider);

		binder.Update();

		var bound = Assert.Single(provider.Bound);
		Assert.Equal("tool", bound.Definition.Id);
	}

	[Fact]
	public void BindsOnlyOnce()
	{
		var provider = new RecordingProvider(ModContentKind.Item);
		var binder = CreateBinder(
			new FakeContentControl(
				new ModContentRegistration("shared.mod", new StubContentDefinition("sword", ModContentKind.Item))),
			new FakeModsControl([
				new ModManifest("shared.mod", "Shared", "1.0.0", NetworkMode.RequiresAllPlayers, null)
			]),
			provider);

		binder.Update();
		binder.Update();

		Assert.Single(provider.Bound);
	}

	/// <summary>
	/// The kind is the one thing the registry cannot judge — it takes any kind
	/// tag — so the provider set is what decides whether a registration can ever
	/// materialize, and the binder is the only place that holds it. A kind no
	/// provider claims is reported at WARNING level naming the kind and the
	/// definition: the mod author reads the default log level, and a Debug line
	/// under an Information-level "registered content" is the silence this
	/// report exists to remove. The other half of the same branch is the skip —
	/// the provider that does NOT claim the kind is never asked.
	/// </summary>
	[Fact]
	public void UnknownKind_IsReportedAsNeverMaterialized()
	{
		var log = new RecordingLogger<ModContentBinder>();
		var provider = new RecordingProvider(ModContentKind.Item);
		var binder = CreateBinder(
			log,
			new FakeContentControl(
				new ModContentRegistration("shared.mod", new StubContentDefinition("future", "future-kind"))),
			new FakeModsControl([
				new ModManifest("shared.mod", "Shared", "1.0.0", NetworkMode.Synchronized, null)
			]),
			provider);

		binder.Update(); // no throw

		var warning = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning);
		Assert.Contains("future-kind", warning.Message, StringComparison.Ordinal);
		Assert.Contains("shared.mod/future", warning.Message, StringComparison.Ordinal);
		Assert.Contains("never materialized", warning.Message, StringComparison.Ordinal);
		Assert.Empty(provider.Bound);
	}

	[Fact]
	public void ProviderException_DoesNotStopOtherEntries()
	{
		var ok = new RecordingProvider(ModContentKind.Item);
		var throwing = new ThrowingProvider(ModContentKind.Recipe);
		var binder = CreateBinder(
			new FakeContentControl(
				new ModContentRegistration("shared.mod", new StubContentDefinition("sword", ModContentKind.Item)),
				new ModContentRegistration("shared.mod", new StubContentDefinition("soup", ModContentKind.Recipe))),
			new FakeModsControl([
				new ModManifest("shared.mod", "Shared", "1.0.0", NetworkMode.Synchronized, null)
			]),
			ok,
			throwing);

		binder.Update();

		var bound = Assert.Single(ok.Bound);
		Assert.Equal("sword", bound.Definition.Id);
	}

	[Fact]
	public void Binder_RoutesRealModContentThroughDi()
	{
		var provider = new RecordingProvider(ModContentKind.Item);
		var (host, _) = TestNode.CreatePair(
			1001,
			2001,
			9001,
			extraRegistrations: s => s.AddSingleton<IContentBindingProvider>(provider));

		Assert.Contains(provider.Bound, b => b.ModId == "test.content" && b.Definition.Id == "wooden.sword");
	}

	private static ModContentBinder CreateBinder(
		FakeContentControl control,
		FakeModsControl mods,
		params IContentBindingProvider[] providers) =>
		CreateBinder(NullLogger<ModContentBinder>.Instance, control, mods, providers);

	private static ModContentBinder CreateBinder(
		ILogger<ModContentBinder> log,
		FakeContentControl control,
		FakeModsControl mods,
		params IContentBindingProvider[] providers) =>
		new(control, mods, providers, log);

	private sealed class FakeContentControl(params ModContentRegistration[] entries) : IModContentControl
	{
		public IReadOnlyList<ModContentRegistration> Entries => entries;
	}

	private sealed class FakeModsControl(IReadOnlyList<ModManifest> manifests) : IModsControl
	{
		public IReadOnlyList<ModManifest> CurrentModManifests => manifests;

		public bool IsDiscoveryComplete => true;

		public void FireModMessageReceived(ulong sender, ModMessageMsg msg)
		{
		}

		public void FireModCommandRequestReceived(ulong sender, ModCommandRequestMsg msg)
		{
		}

		public void FireModCommandResultReceived(ulong sender, ModCommandResultMsg msg)
		{
		}
	}

	private sealed class RecordingProvider(string kind) : IContentBindingProvider
	{
		public string Kind { get; } = kind;

		public List<ModContentRegistration> Bound { get; } = [];

		public bool TryBind(ModContentRegistration registration)
		{
			Bound.Add(registration);
			return true;
		}
	}

	private sealed class ThrowingProvider(string kind) : IContentBindingProvider
	{
		public string Kind { get; } = kind;

		public bool TryBind(ModContentRegistration registration) =>
			throw new InvalidOperationException("boom");
	}
}
