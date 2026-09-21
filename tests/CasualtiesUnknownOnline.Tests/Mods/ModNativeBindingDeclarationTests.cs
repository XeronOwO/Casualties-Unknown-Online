using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// Stage 1 of the declared native-binding tier (decision 204): `[CuoMod]`'s
/// `NativeBinding` is a declared FACT that discovery carries into the manifest
/// and the discovery log line. It is deliberately NOT a permission, NOT a
/// rejection cause, and it does not move the wire — carrying it into the session
/// is its own ticket.
///
/// The declared mods below are healthy on purpose: every TestNode's production
/// ModService scans this assembly, so a declared binding must load exactly like
/// any other ClientOnly mod.
/// </summary>
public class ModNativeBindingDeclarationTests
{
	private const string DeclaredBinding = "Recipe.simpleName (the crafting search predicate)";
	private const string PaddedBinding = "  " + DeclaredBinding + "  ";
	private const string DeclaredModId = "test.nativebinding";

	private static Assembly[] TestAssembly => [typeof(ModNativeBindingDeclarationTests).Assembly];

	private static (ModRegistry Registry, RecordingLogger<ModRegistry> Log) CreateRegistry()
	{
		var log = new RecordingLogger<ModRegistry>();
		return (new ModRegistry(log), log);
	}

	[Fact]
	public void DeclaredBinding_IsDiscoveredAndCarriedOnTheManifest()
	{
		var (registry, _) = CreateRegistry();

		var mod = registry.Discover(TestAssembly).Single(d => d.Manifest.Id == DeclaredModId);

		Assert.Equal(DeclaredBinding, mod.Manifest.NativeBinding);
	}

	[Fact]
	public void DeclaredBinding_AppearsInTheDiscoveryLogLine()
	{
		// The host's log answers "which mod binds the game's own code" without
		// reading any mod's source.
		var (registry, log) = CreateRegistry();

		registry.Discover(TestAssembly);

		Assert.Contains(log.Entries, entry =>
			entry.Level == LogLevel.Information
			&& entry.Message.Contains(DeclaredModId, StringComparison.Ordinal)
			&& entry.Message.Contains($"binds {DeclaredBinding}", StringComparison.Ordinal));
	}

	[Fact]
	public void UndeclaredMod_HasNoBinding_AndTheLogSaysSo()
	{
		var (registry, log) = CreateRegistry();

		var echo = registry.Discover(TestAssembly).Single(d => d.Manifest.Id == "test.echo");

		Assert.Null(echo.Manifest.NativeBinding);
		Assert.Contains(log.Entries, entry =>
			entry.Message.Contains("discovered test.echo", StringComparison.Ordinal)
			&& entry.Message.Contains("binds -", StringComparison.Ordinal));
	}

	[Fact]
	public void WhitespaceOnlyBinding_NormalizesToUndeclared_WithoutRejectingTheMod()
	{
		// A whitespace-only value is a typo for "none", not a declaration — and
		// never a new rejection cause.
		var (registry, _) = CreateRegistry();

		var mod = registry.Discover(TestAssembly).Single(d => d.Manifest.Id == "test.nativebindingblank");

		Assert.Null(mod.Manifest.NativeBinding);
	}

	[Fact]
	public void PaddedBinding_IsTrimmedToTheDeclaredName()
	{
		var (registry, _) = CreateRegistry();

		var mod = registry.Discover(TestAssembly).Single(d => d.Manifest.Id == "test.nativebindingpadded");

		Assert.Equal(DeclaredBinding, mod.Manifest.NativeBinding);
	}

	[Fact]
	public void Declaration_TakesNoPermissionAndNoNetworkContract()
	{
		// The declaration buys visibility only: it is not a grant, so it carries
		// no permission, no network mode and no dependency.
		var (registry, _) = CreateRegistry();

		var mod = registry.Discover(TestAssembly).Single(d => d.Manifest.Id == DeclaredModId);

		Assert.Equal(ModPermission.None, mod.Manifest.Permissions);
		Assert.Equal(NetworkMode.ClientOnly, mod.Manifest.NetworkMode);
		Assert.Empty(mod.Manifest.Dependencies);
	}

	[Fact]
	public void Declaration_IsNeverARejectionCause()
	{
		var (registry, log) = CreateRegistry();

		registry.Discover(TestAssembly);

		Assert.DoesNotContain(log.Entries, entry =>
			entry.Level >= LogLevel.Warning && entry.Message.Contains(DeclaredModId, StringComparison.Ordinal));
	}

	[Fact]
	public void Declaration_DoesNotMoveTheWireShape()
	{
		// Stage 1 is deliberately wire-free: the declaration stays local until the
		// parity ticket carries it onto the handshake (with its own protocol bump).
		var properties = typeof(ModInfoMsg).GetProperties()
			.Select(p => p.Name)
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToArray();

		string[] expected = ["Id", "NetworkMode", "Permissions", "Version"];
		Assert.Equal(expected, properties);
	}

	[Fact]
	public void EmptyBinding_NormalizesToUndeclared_LikeWhitespace()
	{
		// "Blank" means empty OR whitespace-only: neither is a declaration, and
		// neither is a rejection cause.
		var (registry, _) = CreateRegistry();

		var mod = registry.Discover(TestAssembly).Single(d => d.Manifest.Id == "test.nativebindingempty");

		Assert.Null(mod.Manifest.NativeBinding);
	}

	[Fact]
	public void NamespaceAndBinding_Coexist_AndBothRenderInTheDiscoveryLogLine()
	{
		// The one input shape where the new log clause can go wrong: a mod that
		// declares a content namespace AND a native binding.
		var (registry, log) = CreateRegistry();

		var mod = registry.Discover(TestAssembly).Single(d => d.Manifest.Id == "test.nativebindingns");

		Assert.Equal("bindingns", mod.Manifest.Namespace);
		Assert.Equal(DeclaredBinding, mod.Manifest.NativeBinding);
		Assert.Contains(log.Entries, entry =>
			entry.Message.Contains("namespace bindingns", StringComparison.Ordinal)
			&& entry.Message.Contains($"binds {DeclaredBinding}", StringComparison.Ordinal));
	}

	[CuoMod(DeclaredModId, "Native Binding", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		NativeBinding = DeclaredBinding)]
	public sealed class NativeBindingMod : ICuoMod
	{
		public void Bind(IModContext context)
		{
		}

		public void Initialize()
		{
		}

		public void Start()
		{
		}

		public void Update()
		{
		}

		public void Stop()
		{
		}

		public void Dispose()
		{
		}
	}

	[CuoMod("test.nativebindingblank", "Blank Native Binding", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		NativeBinding = "   ")]
	public sealed class BlankNativeBindingMod : ICuoMod
	{
		public void Bind(IModContext context)
		{
		}

		public void Initialize()
		{
		}

		public void Start()
		{
		}

		public void Update()
		{
		}

		public void Stop()
		{
		}

		public void Dispose()
		{
		}
	}

	[CuoMod("test.nativebindingpadded", "Padded Native Binding", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		NativeBinding = PaddedBinding)]
	public sealed class PaddedNativeBindingMod : ICuoMod
	{
		public void Bind(IModContext context)
		{
		}

		public void Initialize()
		{
		}

		public void Start()
		{
		}

		public void Update()
		{
		}

		public void Stop()
		{
		}

		public void Dispose()
		{
		}
	}

	[CuoMod("test.nativebindingempty", "Empty Native Binding", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		NativeBinding = "")]
	public sealed class EmptyNativeBindingMod : ICuoMod
	{
		public void Bind(IModContext context)
		{
		}

		public void Initialize()
		{
		}

		public void Start()
		{
		}

		public void Update()
		{
		}

		public void Stop()
		{
		}

		public void Dispose()
		{
		}
	}

	[CuoMod("test.nativebindingns", "Namespaced Native Binding", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Namespace = "bindingns", NativeBinding = DeclaredBinding)]
	public sealed class NamespacedNativeBindingMod : ICuoMod
	{
		public void Bind(IModContext context)
		{
		}

		public void Initialize()
		{
		}

		public void Start()
		{
		}

		public void Update()
		{
		}

		public void Stop()
		{
		}

		public void Dispose()
		{
		}
	}
}
