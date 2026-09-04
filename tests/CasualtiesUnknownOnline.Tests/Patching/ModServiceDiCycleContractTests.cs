using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Guards the production ModService ↔ GameAdapter DI cycle. ModService injects
/// the Game Adapter through IModEntitySpawner/IModItemSpawner/... seams; if the
/// Game Adapter also depended on ModService, resolving ModService would recurse
/// into GameAdapter (observed as a startup hang). The fix is to inject only
/// ModStatusStore into the adapter, not the whole ModService.
/// </summary>
public class ModServiceDiCycleContractTests
{
	[Fact]
	public void GameAdapter_DoesNotDependOnModService_AndUsesStatusStore()
	{
		var adapter = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.GameAdapter", throwOnError: true);
		var constructor = adapter.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
			.SingleOrDefault()
			?? throw new InvalidOperationException("GameAdapter has no public constructor.");

		Assert.DoesNotContain(constructor.GetParameters(), p => p.ParameterType == typeof(ModService));
		Assert.Contains(constructor.GetParameters(), p => p.ParameterType == typeof(ModStatusStore));
	}
}
