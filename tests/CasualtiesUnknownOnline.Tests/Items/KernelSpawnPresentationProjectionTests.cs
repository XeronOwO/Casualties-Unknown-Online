using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

public class KernelSpawnPresentationProjectionTests
{
	[Fact]
	public void Spawn_PresentationFlowsThroughKernelBatchProjectionToWorldItem()
	{
		var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
		var table = new WorldItemTable();
		WorldItem? projected = null;
		var projection = new KernelBatchItemProjection(
			authority,
			table,
			item => projected = item,
			_ => { },
			(_, _, _, _, _, _, _, _) => { },
			_ => { });

		var ok = authority.TrySpawn(
			1001,
			new ItemIdentity(777, "metalscrap"),
			ItemLocation.World(10f, 20f),
			new CharacterItemMsg { InstanceId = 777, ItemId = "metalscrap", Condition = 1f },
			out var batch,
			out var rejection,
			velocityX: 3.5f,
			velocityY: -2f,
			rotation: 45f,
			freshItemDrop: true,
			angularVelocity: 8f);

		Assert.True(ok, rejection?.Message ?? "spawn rejected without message");
		projection.Apply(batch!);

		Assert.NotNull(projected);
		var value = projected!.Value;
		Assert.True(value.FreshItemDrop);
		Assert.Equal(3.5f, value.Vel.X);
		Assert.Equal(-2f, value.Vel.Y);
		Assert.Equal(45f, value.Rotation);
		Assert.Equal(8f, value.AngularVelocity);
	}
}
