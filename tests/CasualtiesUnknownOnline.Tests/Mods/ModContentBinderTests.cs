using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The generic content binder: it routes opaque content registrations to
/// per-kind providers after mod discovery, and it only binds content from
/// network modes that guarantee all peers have the same static content.
/// </summary>
public class ModContentBinderTests
{
	[Fact]
	public void BindsSharedContentToMatchingProvider()
	{
		var provider = new RecordingProvider(ModContentKind.Item);
		var binder = CreateBinder(
			new FakeContentControl(
				new ModContentRegistration("shared.mod", new ModContentDefinition("sword", ModContentKind.Item, [], 1))),
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
				new ModContentRegistration("host.mod", new ModContentDefinition("sword", ModContentKind.Item, [], 1)),
				new ModContentRegistration("shared.mod", new ModContentDefinition("tool", ModContentKind.Item, [], 1))),
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
				new ModContentRegistration("shared.mod", new ModContentDefinition("sword", ModContentKind.Item, [], 1))),
			new FakeModsControl([
				new ModManifest("shared.mod", "Shared", "1.0.0", NetworkMode.RequiresAllPlayers, null)
			]),
			provider);

		binder.Update();
		binder.Update();

		Assert.Single(provider.Bound);
	}

	[Fact]
	public void UnknownKind_IsSkippedWithoutProvider()
	{
		var binder = CreateBinder(
			new FakeContentControl(
				new ModContentRegistration("shared.mod", new ModContentDefinition("future", "future-kind", [], 1))),
			new FakeModsControl([
				new ModManifest("shared.mod", "Shared", "1.0.0", NetworkMode.Synchronized, null)
			]),
			new RecordingProvider(ModContentKind.Item));

		binder.Update(); // no throw
	}

	[Fact]
	public void ProviderException_DoesNotStopOtherEntries()
	{
		var ok = new RecordingProvider(ModContentKind.Item);
		var throwing = new ThrowingProvider(ModContentKind.Recipe);
		var binder = CreateBinder(
			new FakeContentControl(
				new ModContentRegistration("shared.mod", new ModContentDefinition("sword", ModContentKind.Item, [], 1)),
				new ModContentRegistration("shared.mod", new ModContentDefinition("soup", ModContentKind.Recipe, [], 1))),
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
		new(control, mods, providers, NullLogger<ModContentBinder>.Instance);

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
