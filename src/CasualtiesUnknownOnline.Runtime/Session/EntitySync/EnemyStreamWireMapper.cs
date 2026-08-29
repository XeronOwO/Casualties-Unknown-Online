using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Maps between the runtime enemy entity buffer and the convergent
/// high-frequency wire state stream.
/// </summary>
public static class EnemyStreamWireMapper
{
	public static WireEnemyStreamState ToWireEnemyStreamState(this EnemyEntity entity) => new()
	{
		EntityId = PlayerStreamWireMapper.ToWireEntityId(entity.EntityId),
		Position = ToWireVector2(entity.Position),
		Velocity = ToWireVector2(entity.Velocity),
		Rotation = entity.Rotation,
		Health = entity.Health,
		PresentationFlags = entity.Stunned ? 0x01u : 0u,
		SpiderLegTargets = entity.SpiderLegTargets is { } legs
			? [.. legs.Select(ToWireVector2)]
			: null,
		CrystalWindupAmount = entity.CrystalWindupAmount,
		CrystalLineEnd = entity.CrystalLineEnd is { } line ? ToWireVector2(line) : null,
	};

	public static void ApplyTo(this WireEnemyStreamState wire, EnemyEntity target)
	{
		target.EntityId = PlayerStreamWireMapper.ToNetworkEntityId(wire.EntityId);
		target.Position = ToNetVector2(wire.Position);
		target.Velocity = ToNetVector2(wire.Velocity);
		target.Rotation = wire.Rotation;
		target.Health = wire.Health;
		target.Stunned = (wire.PresentationFlags & 0x01u) != 0;
		target.SpiderLegTargets = wire.SpiderLegTargets is { } legs
			? [.. legs.Select(ToNetVector2)]
			: null;
		target.CrystalWindupAmount = wire.CrystalWindupAmount;
		target.CrystalLineEnd = wire.CrystalLineEnd is { } line ? ToNetVector2(line) : null;
	}

	private static WireVector2 ToWireVector2(NetVector2 value) => new() { X = value.X, Y = value.Y };

	private static NetVector2 ToNetVector2(WireVector2 value) => new(value.X, value.Y);
}
