using System;
using System.IO;
using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The typed wire/command payload for one committed mod-status runtime value.
/// It is a plain data object in Abstractions: no Unity type, no game type, no
/// Runtime dependency. It deliberately carries only the stable key
/// (<c>status id + player SteamId + optional limb slot</c>), the mod-owned
/// schema version, and the mod-owned opaque byte value plus an explicit remove
/// flag. It is NOT a generic JObject snapshot and it does NOT attempt to
/// interpret the value.
///
/// The frame travels over the existing <see cref="IModNetwork"/> channel (the
/// public mod-message frame); the helper surface in
/// <see cref="IModStatusTransport"/> converts between this DTO and the runtime
/// status table. Static status descriptors stay off the wire and are already
/// covered by the mod handshake.
/// </summary>
[DataContract]
public sealed class ModStatusUpdate
{
	/// <summary>The mod-scoped status id declared through <see cref="IModStatusRuntime.TryDeclare"/>.</summary>
	[DataMember(Order = 1)]
	public string StatusId { get; set; } = string.Empty;

	/// <summary>Body-level or limb-level — required so the receiver knows which table to touch.</summary>
	[DataMember(Order = 2)]
	public ModStatusScope Scope { get; set; } = ModStatusScope.Body;

	/// <summary>The player whose body/limb carries this status.</summary>
	[DataMember(Order = 3)]
	public ulong PlayerSteamId { get; set; }

	/// <summary>
	/// The limb slot for <see cref="ModStatusScope.Limb"/> updates. Body
	/// updates use <c>-1</c> as the sentinel (never a valid limb slot).
	/// </summary>
	[DataMember(Order = 4)]
	public int LimbSlot { get; set; } = -1;

	/// <summary>The mod-owned schema version declared for this status slot.</summary>
	[DataMember(Order = 5)]
	public int SchemaVersion { get; set; } = 1;

	/// <summary>The mod-owned value payload. Illegal/absent for a removal frame.</summary>
	[DataMember(Order = 6)]
	public byte[] Value { get; set; } = [];

	/// <summary>True when this frame clears the status value on the receiver; false when it writes <see cref="Value"/>.</summary>
	[DataMember(Order = 7)]
	public bool Remove { get; set; }

	/// <summary>Creates a body status set frame.</summary>
	public static ModStatusUpdate ForBody(string statusId, ulong playerSteamId, int schemaVersion, byte[] value) =>
		new()
		{
			StatusId = statusId,
			Scope = ModStatusScope.Body,
			PlayerSteamId = playerSteamId,
			LimbSlot = -1,
			SchemaVersion = schemaVersion,
			Value = value,
			Remove = false,
		};

	/// <summary>Creates a limb status set frame.</summary>
	public static ModStatusUpdate ForLimb(string statusId, ulong playerSteamId, int limbSlot, int schemaVersion, byte[] value) =>
		new()
		{
			StatusId = statusId,
			Scope = ModStatusScope.Limb,
			PlayerSteamId = playerSteamId,
			LimbSlot = limbSlot,
			SchemaVersion = schemaVersion,
			Value = value,
			Remove = false,
		};

	/// <summary>Creates a body status removal frame.</summary>
	public static ModStatusUpdate RemoveBody(string statusId, ulong playerSteamId, int schemaVersion) =>
		new()
		{
			StatusId = statusId,
			Scope = ModStatusScope.Body,
			PlayerSteamId = playerSteamId,
			LimbSlot = -1,
			SchemaVersion = schemaVersion,
			Value = [],
			Remove = true,
		};

	/// <summary>Creates a limb status removal frame.</summary>
	public static ModStatusUpdate RemoveLimb(string statusId, ulong playerSteamId, int limbSlot, int schemaVersion) =>
		new()
		{
			StatusId = statusId,
			Scope = ModStatusScope.Limb,
			PlayerSteamId = playerSteamId,
			LimbSlot = limbSlot,
			SchemaVersion = schemaVersion,
			Value = [],
			Remove = true,
		};

	/// <summary>Serialize this update into the opaque mod-message payload.</summary>
	public byte[] ToPayload()
	{
		using var stream = new MemoryStream();
		var serializer = new DataContractSerializer(typeof(ModStatusUpdate));
		serializer.WriteObject(stream, this);
		return stream.ToArray();
	}

	/// <summary>Deserialize a status update payload. Returns null when the payload is not a valid ModStatusUpdate.</summary>
	public static ModStatusUpdate? FromPayload(byte[] payload)
	{
		if (payload is null)
		{
			return null;
		}

		try
		{
			using var stream = new MemoryStream(payload);
			var serializer = new DataContractSerializer(typeof(ModStatusUpdate));
			return serializer.ReadObject(stream) as ModStatusUpdate;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
