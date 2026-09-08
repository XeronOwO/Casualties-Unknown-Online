using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Reflection;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The discovery registry as a pure judge: given an assembly list it yields
/// the validated manifests and skips every malformed candidate WITH a log (one
/// broken mod never blocks the scan). The bad candidates live here as nested
/// types — they are also scanned by the production ModService of every TestNode
/// in this process, and the registry skipping them is exactly the behavior
/// these tests lock.
/// </summary>
public class ModDiscoveryTests
{
	private static ModRegistry CreateRegistry() => new(NullLogger<ModRegistry>.Instance);

	private static Assembly[] TestAssembly => [typeof(ModDiscoveryTests).Assembly];

	[Fact]
	public void HealthyMod_DiscoveredWithFullManifest()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		var echo = discovered.Single(d => d.Manifest.Id == "test.echo");
		Assert.Equal("Test Echo", echo.Manifest.DisplayName);
		Assert.Equal("1.0.0", echo.Manifest.Version);
		Assert.Equal(NetworkMode.Synchronized, echo.Manifest.NetworkMode);
		Assert.Equal(typeof(TestEchoMod), echo.Type);
	}

	[Fact]
	public void UnspecifiedNetworkMode_Rejected()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.unspecified");
	}

	[Fact]
	public void MissingParameterlessConstructor_Rejected()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.noctor");
	}

	[Fact]
	public void AbstractMod_Rejected()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.abstract");
	}

	[Fact]
	public void CuoModWithoutICuoMod_Ignored()
	{
		// The filter only reads the attribute after the ICuoMod check — a class
		// that is not a CUO mod type is not ours, whatever it declares.
		var discovered = CreateRegistry().Discover(TestAssembly);

		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.notamod");
	}

	[Fact]
	public void DuplicatedId_OnlyFirstWins()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		Assert.Single(discovered, d => d.Manifest.Id == "test.duplicate");
	}

	[Fact]
	public void Discovery_IsIdempotent()
	{
		var registry = CreateRegistry();
		var first = registry.Discover(TestAssembly);
		var second = registry.Discover(TestAssembly);

		Assert.Equal(first.Select(d => d.Manifest.Id), second.Select(d => d.Manifest.Id));
	}

	[Fact]
	public void EmptyAssemblySet_YieldsEmptyList()
	{
		var discovered = CreateRegistry().Discover([]);

		Assert.Empty(discovered);
	}

	[Fact]
	public void CurrentModInfos_MirrorsTheDiscoveredManifests()
	{
		var registry = CreateRegistry();
		registry.Discover(TestAssembly);

		var infos = registry.CurrentModInfos();
		var echo = infos.Single(i => i.Id == "test.echo");
		Assert.Equal("1.0.0", echo.Version);
		Assert.Equal(NetworkMode.Synchronized, echo.NetworkMode);
	}

	[Fact]
	public void InvalidSemVerVersion_Rejected()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.badsemver");
	}

	[Fact]
	public void InvalidPermissionsForMode_Rejected()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.badperm");
		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.unknownperm");
	}

	[Fact]
	public void Dependencies_AreLoadedInTopologicalOrder()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);
		var ids = discovered.Select(d => d.Manifest.Id).ToList();
		var baseIndex = ids.IndexOf("test.depbase");
		var childIndex = ids.IndexOf("test.depchild");
		var grandIndex = ids.IndexOf("test.depgrand");

		Assert.True(baseIndex >= 0 && childIndex > baseIndex && grandIndex > childIndex,
			"dependencies must load before their dependents (base < child < grand)");
		Assert.Equal(["test.depbase"], discovered[childIndex].Manifest.Dependencies);
	}

	[Fact]
	public void UnsatisfiableDependencies_Rejected()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.depmissing");
		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.depself");
		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.depduplicate");
		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.depcyclea");
		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.depcycleb");
		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.deptransitive");
	}

	[Fact]
	public void HealthyManifest_CarriesPermissionsAndDependencies()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		var echo = discovered.Single(d => d.Manifest.Id == "test.echo");
		Assert.Equal(ModPermission.SendNetworkMessage, echo.Manifest.Permissions);
		Assert.Empty(echo.Manifest.Dependencies);
	}

	[Fact]
	public void DeclaredNamespace_IsCarriedInTheManifest()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		var namespaced = discovered.Single(d => d.Manifest.Id == "test.nsok");
		Assert.Equal("testns", namespaced.Manifest.Namespace);
		Assert.Null(discovered.Single(d => d.Manifest.Id == "test.echo").Manifest.Namespace);
	}

	[Fact]
	public void ReservedOrInvalidNamespace_Rejected()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.nsreserved");
		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.nsinvalid");
		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.nsempty");
		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.nsblank");
	}

	[Fact]
	public void RejectedMod_DoesNotConsumeItsModId()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		// The first test.idreclaim declaration is rejected for a missing
		// dependency; the second declaration of the same id must still load
		// (a rejected candidate may not consume the id).
		var winner = Assert.Single(discovered, d => d.Manifest.Id == "test.idreclaim");
		Assert.Equal("reclaimns", winner.Manifest.Namespace);
	}

	[Fact]
	public void DuplicatedNamespace_OnlyFirstWins()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		var winner = Assert.Single(discovered, d => d.Manifest.Namespace == "testdupns");
		Assert.Equal("test.nsdup.a", winner.Manifest.Id);
	}

	[Fact]
	public void RejectedMod_DoesNotConsumeItsNamespace()
	{
		var discovered = CreateRegistry().Discover(TestAssembly);

		// test.nslost declares lostns but is rejected for a missing dependency;
		// the namespace must remain available to the valid test.nsrecover.
		Assert.DoesNotContain(discovered, d => d.Manifest.Id == "test.nslost");
		Assert.Contains(discovered, d => d.Manifest.Id == "test.nsrecover" && d.Manifest.Namespace == "lostns");
	}

	// ---- The malformed candidates (nested — they belong to this test) ----

	[CuoMod("test.unspecified", "No Mode", "1.0.0")] // NetworkMode defaults to Unspecified — fail-closed
	public sealed class UnspecifiedModeMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.noctor", "No Ctor", "1.0.0", NetworkMode = NetworkMode.ClientOnly)]
	public sealed class NoParameterlessCtorMod(int value) : ICuoMod
	{
		// No public parameterless constructor — Activator would fail.
		public int Value { get; } = value;

		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.abstract", "Abstract", "1.0.0", NetworkMode = NetworkMode.ClientOnly)]
	public abstract class AbstractMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.notamod", "Not A Mod", "1.0.0", NetworkMode = NetworkMode.ClientOnly)]
	public sealed class CuoModButNotICuoMod
	{
		// Declares the attribute but is not an ICuoMod — the filter never reads it.
	}

	[CuoMod("test.duplicate", "Duplicate A", "1.0.0", NetworkMode = NetworkMode.ClientOnly)]
	public sealed class DuplicateIdMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.duplicate", "Duplicate B", "2.0.0", NetworkMode = NetworkMode.Synchronized)]
	public sealed class DuplicateIdMod2 : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	// ---- Phase 4b malformed/ordering candidates ----

	[CuoMod("test.badsemver", "Bad SemVer", "not-semver", NetworkMode = NetworkMode.ClientOnly)]
	public sealed class BadSemVerMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.badperm", "Bad Permissions", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Permissions = ModPermission.WriteGameState)]
	public sealed class BadPermissionMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.unknownperm", "Unknown Permissions", "1.0.0", NetworkMode = NetworkMode.HostOnly,
		Permissions = (ModPermission)(1 << 20))]
	public sealed class UnknownPermissionMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.depbase", "Dependency Base", "1.0.0", NetworkMode = NetworkMode.ClientOnly)]
	public sealed class DependencyBaseMod : ICuoMod
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

	[CuoMod("test.depchild", "Dependency Child", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Dependencies = new[] { "test.depbase" })]
	public sealed class DependencyChildMod : ICuoMod
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

	[CuoMod("test.depgrand", "Dependency Grandchild", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Dependencies = new[] { "test.depchild" })]
	public sealed class DependencyGrandchildMod : ICuoMod
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

	[CuoMod("test.depmissing", "Dependency Missing", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Dependencies = new[] { "test.doesnotexist" })]
	public sealed class DependencyMissingMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.depself", "Dependency Self", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Dependencies = new[] { "test.depself" })]
	public sealed class DependencySelfMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.depduplicate", "Dependency Duplicate", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Dependencies = new[] { "test.depbase", "test.depbase" })]
	public sealed class DependencyDuplicateMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.depcyclea", "Dependency Cycle A", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Dependencies = new[] { "test.depcycleb" })]
	public sealed class DependencyCycleAMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.depcycleb", "Dependency Cycle B", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Dependencies = new[] { "test.depcyclea" })]
	public sealed class DependencyCycleBMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}
	[CuoMod("test.deptransitive", "Dependency Transitive", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Dependencies = new[] { "test.depmissing" })]
	public sealed class DependencyTransitiveMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	// ---- Namespaced-id candidates (nested — they belong to this test) ----

	[CuoMod("test.nsok", "Namespaced OK", "1.0.0", NetworkMode = NetworkMode.ClientOnly, Namespace = "testns")]
	public sealed class NamespacedMod : ICuoMod
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

	[CuoMod("test.nsreserved", "Reserved Namespace", "1.0.0", NetworkMode = NetworkMode.ClientOnly, Namespace = "cu")]
	public sealed class ReservedNamespaceMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.nsinvalid", "Invalid Namespace", "1.0.0", NetworkMode = NetworkMode.ClientOnly, Namespace = "Test NS!")]
	public sealed class InvalidNamespaceMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.nsdup.a", "Duplicate Namespace A", "1.0.0", NetworkMode = NetworkMode.ClientOnly, Namespace = "testdupns")]
	public sealed class DuplicateNamespaceAMod : ICuoMod
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

	[CuoMod("test.nsdup.b", "Duplicate Namespace B", "1.0.0", NetworkMode = NetworkMode.ClientOnly, Namespace = "testdupns")]
	public sealed class DuplicateNamespaceBMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.nslost", "Namespace Loser", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Namespace = "lostns", Dependencies = new[] { "test.depmissing" })]
	public sealed class RejectedNamespaceOwnerMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.nsrecover", "Namespace Recover", "1.0.0", NetworkMode = NetworkMode.ClientOnly, Namespace = "lostns")]
	public sealed class RecoveredNamespaceMod : ICuoMod
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

	[CuoMod("test.nsempty", "Empty Namespace", "1.0.0", NetworkMode = NetworkMode.ClientOnly, Namespace = "")]
	public sealed class EmptyNamespaceMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.nsblank", "Blank Namespace", "1.0.0", NetworkMode = NetworkMode.ClientOnly, Namespace = "   ")]
	public sealed class BlankNamespaceMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.idreclaim", "Rejected Id Owner", "1.0.0", NetworkMode = NetworkMode.ClientOnly,
		Namespace = "reclaimns", Dependencies = new[] { "test.depmissing" })]
	public sealed class RejectedIdOwnerMod : ICuoMod
	{
		public void Bind(IModContext context) => throw new InvalidOperationException("must never load");

		public void Initialize() => throw new InvalidOperationException("must never load");

		public void Start() => throw new InvalidOperationException("must never load");

		public void Update() => throw new InvalidOperationException("must never load");

		public void Stop() => throw new InvalidOperationException("must never load");

		public void Dispose() => throw new InvalidOperationException("must never load");
	}

	[CuoMod("test.idreclaim", "Reclaimed Id", "1.0.0", NetworkMode = NetworkMode.ClientOnly, Namespace = "reclaimns")]
	public sealed class ReclaimedIdMod : ICuoMod
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
