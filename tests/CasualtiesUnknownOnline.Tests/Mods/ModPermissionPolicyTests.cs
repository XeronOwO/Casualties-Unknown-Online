using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The pure permission judge: every flag in <see cref="ModPermission.All"/>
/// is defined, None is valid, unknown bits are rejected, and the five
/// host/state permissions are refused on ClientOnly/Cosmetic (architecture.md
/// §5.3: client-only mods must not register sync objects/commands).
/// </summary>
public class ModPermissionPolicyTests
{
	[Fact]
	public void None_IsDefinedAndValidEverywhere()
	{
		Assert.True(ModPermissionPolicy.IsValidFor(NetworkMode.ClientOnly, ModPermission.None));
		Assert.True(ModPermissionPolicy.IsValidFor(NetworkMode.HostOnly, ModPermission.None));
	}

	[Fact]
	public void AllDeclaredPermissions_AreDefined() =>
		Assert.True(ModPermissionPolicy.IsDefined(ModPermission.All));

	[Theory]
	[InlineData((int)ModPermission.ReadGameState)]
	[InlineData((int)ModPermission.WriteGameState)]
	[InlineData((int)ModPermission.SpawnEntity)]
	[InlineData((int)ModPermission.SendNetworkMessage)]
	[InlineData((int)ModPermission.RegisterContent)]
	[InlineData((int)ModPermission.RegisterCommand)]
	[InlineData((int)ModPermission.ExecuteHostAction)]
	[InlineData((int)ModPermission.AccessNativeApi)]
	public void EveryPermission_IsValidOnAHostMode(int permission) =>
		Assert.True(ModPermissionPolicy.IsValidFor(NetworkMode.HostOnly, (ModPermission)permission));

	[Theory]
	[InlineData((int)ModPermission.WriteGameState)]
	[InlineData((int)ModPermission.SpawnEntity)]
	[InlineData((int)ModPermission.RegisterContent)]
	[InlineData((int)ModPermission.RegisterCommand)]
	[InlineData((int)ModPermission.ExecuteHostAction)]
	public void HostOrStatePermission_IsRefusedOnClientOnly(int permission)
	{
		Assert.False(ModPermissionPolicy.IsValidFor(NetworkMode.ClientOnly, (ModPermission)permission), $"{permission} is a host/state permission");
		Assert.False(ModPermissionPolicy.IsValidFor(NetworkMode.Cosmetic, (ModPermission)permission), $"{permission} is a host/state permission");
	}

	[Fact]
	public void LocalPermissions_AreAllowedOnClientOnly()
	{
		var local = ModPermission.ReadGameState | ModPermission.SendNetworkMessage | ModPermission.AccessNativeApi;
		Assert.True(ModPermissionPolicy.IsValidFor(NetworkMode.ClientOnly, local));
	}

	[Fact]
	public void UnknownBit_IsRejected()
	{
		var unknown = (ModPermission)(1 << 20);
		Assert.False(ModPermissionPolicy.IsDefined(unknown));
		Assert.False(ModPermissionPolicy.IsValidFor(NetworkMode.HostOnly, unknown));
		Assert.False(ModPermissionPolicy.IsValidFor(NetworkMode.ClientOnly, ModPermission.None | unknown));
	}
}
