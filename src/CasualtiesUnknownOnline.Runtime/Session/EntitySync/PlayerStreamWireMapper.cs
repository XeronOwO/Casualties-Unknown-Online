using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Maps between the runtime player entity buffer and the convergent
/// high-frequency wire state stream.
/// </summary>
public static class PlayerStreamWireMapper
{
	public static WirePlayerStreamState ToWirePlayerStreamState(this PlayerEntity entity) => new()
	{
		EntityId = ToWireEntityId(entity.EntityId),
		Position = ToWireVector2(entity.Position),
		LookPos = ToWireVector2(entity.LookPos),
		LookOverridePos = entity.LookOverridePos is { } look ? ToWireVector2(look) : null,
		LookOverrideTime = entity.LookOverrideTime,
		EyeScareTime = entity.EyeScareTime,
		EyePanicTime = entity.EyePanicTime,
		EyeCloseTime = entity.EyeCloseTime,
		Velocity = ToWireVector2(entity.Velocity),
		Flags = (byte)(
			(entity.IsRight ? 0x01 : 0) | (entity.Standing ? 0x02 : 0) |
			(entity.Alive ? 0x04 : 0) | (entity.Conscious ? 0x08 : 0) |
			(entity.Crouching ? 0x10 : 0) | (entity.Sitting ? 0x20 : 0) |
			(entity.Sleeping ? 0x40 : 0) | (entity.Climbing ? 0x80 : 0)),
		ExtendedFlags = (entity.IsAttacking ? 0x01u : 0u) |
			(entity.SlidingLeft ? 0x02u : 0u) |
			(entity.SlidingRight ? 0x04u : 0u),
		SwingSeq = entity.SwingSeq,
		WorkoutType = entity.WorkoutType,
		NapVariant = entity.NapVariant,
		DogShakeIntensity = entity.DogShakeIntensity,
		LimbPoses = entity.LimbPoses?.ConvertAll(p => new WirePlayerLimbPose
		{
			Index = p.Index,
			WorldPosition = ToWireVector2(p.WorldPosition),
			RotationZ = p.RotationZ,
		}),
	};

	public static void ApplyTo(this WirePlayerStreamState wire, PlayerEntity target)
	{
		target.Position = ToNetVector2(wire.Position);
		target.LookPos = ToNetVector2(wire.LookPos);
		target.LookOverridePos = wire.LookOverridePos is { } look ? ToNetVector2(look) : null;
		target.LookOverrideTime = wire.LookOverrideTime;
		target.EyeScareTime = wire.EyeScareTime;
		target.EyePanicTime = wire.EyePanicTime;
		target.EyeCloseTime = wire.EyeCloseTime;
		target.Velocity = ToNetVector2(wire.Velocity);
		target.IsRight = (wire.Flags & 0x01) != 0;
		target.Standing = (wire.Flags & 0x02) != 0;
		target.Alive = (wire.Flags & 0x04) != 0;
		target.Conscious = (wire.Flags & 0x08) != 0;
		target.Crouching = (wire.Flags & 0x10) != 0;
		target.Sitting = (wire.Flags & 0x20) != 0;
		target.Sleeping = (wire.Flags & 0x40) != 0;
		target.Climbing = (wire.Flags & 0x80) != 0;
		target.IsAttacking = (wire.ExtendedFlags & 0x01u) != 0;
		target.SlidingLeft = (wire.ExtendedFlags & 0x02u) != 0;
		target.SlidingRight = (wire.ExtendedFlags & 0x04u) != 0;
		target.SwingSeq = wire.SwingSeq;
		target.WorkoutType = wire.WorkoutType;
		target.NapVariant = wire.NapVariant;
		target.DogShakeIntensity = wire.DogShakeIntensity;
		target.LimbPoses = wire.LimbPoses?.ConvertAll(p => new PlayerLimbPose
		{
			Index = p.Index,
			WorldPosition = ToNetVector2(p.WorldPosition),
			RotationZ = p.RotationZ,
		});
	}

	public static WireEntityId ToWireEntityId(NetworkEntityId id) =>
		new()
		{
			Epoch = id.Epoch,
			Counter = id.Counter,
			Generation = id.Generation,
		};

	public static NetworkEntityId ToNetworkEntityId(WireEntityId id) =>
		new(id.Epoch, id.Counter, id.Generation);

	private static WireVector2 ToWireVector2(NetVector2 value) => new() { X = value.X, Y = value.Y };

	private static NetVector2 ToNetVector2(WireVector2 value) => new(value.X, value.Y);
}
