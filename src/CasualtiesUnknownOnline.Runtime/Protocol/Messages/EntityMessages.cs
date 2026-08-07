using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Wire form of <see cref="NetVector2"/>. Position fields are always
/// present on the wire (the old binary layout wrote (0,0) for missing values).</summary>
[ProtoContract]
public sealed class NetVector2Msg
{
	public NetVector2Msg()
	{
	}

	public NetVector2Msg(float x, float y)
	{
		X = x;
		Y = y;
	}

	[ProtoMember(1)]
	public float X { get; set; }

	[ProtoMember(2)]
	public float Y { get; set; }

	public static NetVector2Msg From(NetVector2 v) => new(v.X, v.Y);

	public NetVector2 ToNetVector2() => new(X, Y);
}

/// <summary>Wire form of <see cref="NetworkEntityId"/> (session epoch + host allocation counter + generation).</summary>
[ProtoContract]
public sealed class NetworkEntityIdMsg
{
	public NetworkEntityIdMsg()
	{
	}

	public NetworkEntityIdMsg(ulong epoch, uint counter, byte generation)
	{
		Epoch = epoch;
		Counter = counter;
		Generation = generation;
	}

	[ProtoMember(1)]
	public ulong Epoch { get; set; }

	[ProtoMember(2)]
	public uint Counter { get; set; }

	[ProtoMember(3)]
	public uint Generation { get; set; }

	public static NetworkEntityIdMsg From(NetworkEntityId id) => new(id.Epoch, id.Counter, id.Generation);

	public NetworkEntityId ToNetworkEntityId() => new(Epoch, Counter, (byte)Generation);
}

/// <summary>One entity's full authoritative state: identity + position/look/velocity
/// + the packed pose flags (same bit layout as the old WriteEntity).</summary>
[ProtoContract]
public sealed class EntityStateMsg
{
	[ProtoMember(1)]
	public NetworkEntityIdMsg Id { get; set; } = new();

	[ProtoMember(2)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(3)]
	public NetVector2Msg LookPos { get; set; } = new();

	[ProtoMember(4)]
	public NetVector2Msg Velocity { get; set; } = new();

	[ProtoMember(5)]
	public byte Flags { get; set; }

	public static EntityStateMsg From(PlayerEntity entity) => new()
	{
		Id = NetworkEntityIdMsg.From(entity.EntityId),
		Position = NetVector2Msg.From(entity.Position),
		LookPos = NetVector2Msg.From(entity.LookPos),
		Velocity = NetVector2Msg.From(entity.Velocity),
		Flags = (byte)(
			(entity.IsRight ? 0x01 : 0) | (entity.Standing ? 0x02 : 0) |
			(entity.Alive ? 0x04 : 0) | (entity.Conscious ? 0x08 : 0) | (entity.Crouching ? 0x10 : 0) |
			(entity.Sitting ? 0x20 : 0) | (entity.Sleeping ? 0x40 : 0) | (entity.Climbing ? 0x80 : 0)),
	};

	/// <summary>Applies the state onto a live entity buffer (values + flags).</summary>
	public void ApplyTo(PlayerEntity target)
	{
		target.Position = Position.ToNetVector2();
		target.LookPos = LookPos.ToNetVector2();
		target.Velocity = Velocity.ToNetVector2();
		target.IsRight = (Flags & 0x01) != 0;
		target.Standing = (Flags & 0x02) != 0;
		target.Alive = (Flags & 0x04) != 0;
		target.Conscious = (Flags & 0x08) != 0;
		target.Crouching = (Flags & 0x10) != 0;
		target.Sitting = (Flags & 0x20) != 0;
		target.Sleeping = (Flags & 0x40) != 0;
		target.Climbing = (Flags & 0x80) != 0;
	}
}

/// <summary>Host → guest: the authoritative batch of entity states (20 Hz).</summary>
[ProtoContract]
public sealed class PlayerStateMsg
{
	[ProtoMember(1)]
	public List<EntityStateMsg> Entities { get; set; } = [];
}

/// <summary>Guest → host: the guest's locally simulated state (no host-side simulation).</summary>
[ProtoContract]
public sealed class PlayerStateReportMsg
{
	[ProtoMember(1)]
	public EntityStateMsg Entity { get; set; } = new();
}

/// <summary>Host → guest: join confirmation with both entity ids and the host position (clone anchor).</summary>
[ProtoContract]
public sealed class PlayerJoinMsg
{
	[ProtoMember(1)]
	public ulong HostSteamId { get; set; }

	[ProtoMember(2)]
	public NetworkEntityIdMsg HostEntityId { get; set; } = new();

	[ProtoMember(3)]
	public NetworkEntityIdMsg GuestEntityId { get; set; } = new();

	[ProtoMember(4)]
	public NetVector2Msg HostPosition { get; set; } = new();
}
