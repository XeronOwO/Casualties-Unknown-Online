using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The Game Adapter native-API contract (Phase 4 Mod API remainder). The test
/// project never compile-references GameAdapter (it binds Unity/game
/// assemblies), so this locks the implementation shape reflectively: the
/// adapter must implement the Runtime seam, register the documented local-player
/// operation, and own a framework-DTO result type that does not leak Unity.
/// </summary>
public class GameAdapterNativeApiContractTests
{
	[Fact]
	public void GameAdapter_ImplementsModNativeApiProvider()
	{
		var adapter = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.GameAdapter",
			throwOnError: true)!;

		Assert.True(typeof(IModNativeApiProvider).IsAssignableFrom(adapter),
			"GameAdapter must implement IModNativeApiProvider (the Runtime → Game Adapter native-API seam).");
	}

	[Fact]
	public void GameAdapter_OwnsTheLocalPlayerStateDto()
	{
		var adapter = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.GameAdapter",
			throwOnError: true)!;

		var dto = adapter.GetNestedTypes(BindingFlags.NonPublic)
			.SingleOrDefault(t => t.Name == "NativeLocalPlayerState"
				&& typeof(IModNativeLocalPlayerState).IsAssignableFrom(t));

		Assert.NotNull(dto);
	}

	[Fact]
	public void NativeApiOperationId_IsThePublicContract() =>
		Assert.Equal("local.player.state", ModNativeApiOperations.LocalPlayerState);
}
