using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Protocol;

/// <summary>
/// The frame layer: [msgId:1][protobuf payload]. These tests are deliberately
/// transport-free — they lock the wire format contract (the first layer the
/// FakeTransport pipeline builds on) without touching any seam.
/// </summary>
public class NetPacketTests
{
	[Fact]
	public void Encode_WritesMsgIdThenPayload()
	{
		var frame = NetPacket.Encode(NetMsg.Handshake, new HandshakeMsg { Protocol = ProtocolVersion.Current });

		Assert.Equal((byte)NetMsg.Handshake, frame[0]);
		Assert.True(frame.Length > 1, "payload must follow the msgId byte");
	}

	[Fact]
	public void EncodeDecode_RoundTripsMessage()
	{
		var msg = new HandshakeMsg { Protocol = ProtocolVersion.Current };

		var decoded = NetPacket.DecodePayload<HandshakeMsg>(NetPacket.Encode(NetMsg.Handshake, msg));

		Assert.Equal(ProtocolVersion.Current, decoded.Protocol);
	}

	[Fact]
	public void Encode_WithoutPayload_IsSingleByteFrame()
	{
		var frame = NetPacket.Encode(NetMsg.HandshakeAckAck);

		Assert.Equal([(byte)NetMsg.HandshakeAckAck], frame);
	}

	[Fact]
	public void Handshake_PlayerColor_RoundTrips()
	{
		var msg = new HandshakeMsg
		{
			Protocol = ProtocolVersion.Current,
			HasColor = true,
			Color = new NetColorRgbaMsg(0.1f, 0.2f, 0.3f, 1f),
		};

		var decoded = NetPacket.DecodePayload<HandshakeMsg>(NetPacket.Encode(NetMsg.Handshake, msg));

		Assert.True(decoded.HasColor);
		Assert.Equal(0.1f, decoded.Color.R);
		Assert.Equal(0.2f, decoded.Color.G);
		Assert.Equal(0.3f, decoded.Color.B);
		Assert.Equal(1f, decoded.Color.A);
	}

	[Fact]
	public void HandshakeAck_PlayerColor_RoundTrips()
	{
		var msg = new HandshakeAckMsg
		{
			Protocol = ProtocolVersion.Current,
			HasColor = true,
			Color = new NetColorRgbaMsg(0.4f, 0.5f, 0.6f, 1f),
		};

		var decoded = NetPacket.DecodePayload<HandshakeAckMsg>(NetPacket.Encode(NetMsg.HandshakeAck, msg));

		Assert.True(decoded.HasColor);
		Assert.Equal(0.4f, decoded.Color.R);
		Assert.Equal(0.5f, decoded.Color.G);
		Assert.Equal(0.6f, decoded.Color.B);
		Assert.Equal(1f, decoded.Color.A);
	}

	[Fact]
	public void PlayerJoin_PlayerColor_RoundTrips()
	{
		var msg = new PlayerJoinMsg
		{
			GuestSteamId = 77ul,
			HasColor = true,
			Color = new NetColorRgbaMsg(0.7f, 0.8f, 0.9f, 1f),
		};

		var decoded = NetPacket.DecodePayload<PlayerJoinMsg>(NetPacket.Encode(NetMsg.PlayerJoin, msg));

		Assert.True(decoded.HasColor);
		Assert.Equal(0.7f, decoded.Color.R);
		Assert.Equal(0.8f, decoded.Color.G);
		Assert.Equal(0.9f, decoded.Color.B);
		Assert.Equal(1f, decoded.Color.A);
	}

	[Fact]
	public void PlayerColorUpdate_RoundTrips()
	{
		var msg = new PlayerColorUpdateMsg
		{
			SteamId = 42ul,
			HasColor = true,
			Color = new NetColorRgbaMsg(0.2f, 0.6f, 0.8f, 1f),
		};

		var decoded = NetPacket.DecodePayload<PlayerColorUpdateMsg>(NetPacket.Encode(NetMsg.PlayerColorUpdate, msg));

		Assert.Equal(42ul, decoded.SteamId);
		Assert.True(decoded.HasColor);
		Assert.Equal(0.2f, decoded.Color.R);
		Assert.Equal(0.6f, decoded.Color.G);
		Assert.Equal(0.8f, decoded.Color.B);
		Assert.Equal(1f, decoded.Color.A);
	}

	[Fact]
	public void DecodePayload_ZeroValueOmission_RoundTripsToDefault()
	{
		// protobuf-net omits zero values on the wire — a zero field
		// deserializes back to its default. Round-trip of zero must be stable.
		var decoded = NetPacket.DecodePayload<HandshakeMsg>(NetPacket.Encode(NetMsg.Handshake, new HandshakeMsg()));

		Assert.Equal(0, decoded.Protocol);
		Assert.NotNull(decoded.Scene);
	}

	[Fact]
	public void CharacterData_PositionField_RoundTrips()
	{
		// The reconnect restore's leave-spot field (ProtoMember 7): a null
		// position (old-version sender) stays null, a real one round-trips
		// exactly — the restore's position apply depends on it.
		var msg = new CharacterDataMsg { Position = new NetVector2Msg(12.5f, -34.25f) };

		var decoded = NetPacket.DecodePayload<CharacterDataMsg>(NetPacket.Encode(NetMsg.CharacterData, msg));

		Assert.NotNull(decoded.Position);
		Assert.Equal(12.5f, decoded.Position.X);
		Assert.Equal(-34.25f, decoded.Position.Y);

		var without = NetPacket.DecodePayload<CharacterDataMsg>(NetPacket.Encode(NetMsg.CharacterData, new CharacterDataMsg()));
		Assert.Null(without.Position); // the null claim must survive the wire (message field — omitted, decodes to null)
	}

	[Fact]
	public void CraftReport_ZeroValueEnums_RoundTripTransparently()
	{
		// The craft wire discipline: Kind=Craft and Disposition=Destroyed are
		// zero (omitted on the wire by protobuf) — the omission must decode
		// back to the SAME semantic defaults (the default enum value is the
		// semantic default).
		var msg = new CraftReportMsg
		{
			Kind = CraftOperationKind.Craft,
			Entries =
			[
				new CraftEntryMsg
				{
					Disposition = CraftEntryDisposition.Destroyed,
					Item = new CharacterItemMsg { InstanceId = 42, ItemId = "cloth", Condition = 0.5f },
				},
				new CraftEntryMsg
				{
					Disposition = CraftEntryDisposition.Changed,
					Item = new CharacterItemMsg { InstanceId = 43, ItemId = "knife", Condition = 0.6f },
				},
			],
			Products = [new CharacterItemMsg { InstanceId = 44, ItemId = "bandage", Condition = 1f, SlotIndex = 3 }],
		};

		var decoded = NetPacket.DecodePayload<CraftReportMsg>(NetPacket.Encode(NetMsg.CraftReport, msg));

		Assert.Equal(CraftOperationKind.Craft, decoded.Kind);
		Assert.Equal(2, decoded.Entries.Count);
		Assert.Equal(CraftEntryDisposition.Destroyed, decoded.Entries[0].Disposition);
		Assert.Equal(42ul, decoded.Entries[0].Item.InstanceId);
		Assert.Equal(CraftEntryDisposition.Changed, decoded.Entries[1].Disposition);
		Assert.Equal(0.6f, decoded.Entries[1].Item.Condition);
		Assert.Single(decoded.Products);
		Assert.Equal(44ul, decoded.Products[0].InstanceId);
		Assert.Equal(3, decoded.Products[0].SlotIndex);
	}

	[Fact]
	public void RecipeUnlock_IndexZero_RoundTrips()
	{
		// RecipeIndex 0 is a VALID index (blueprints roll Range(0, Count)) —
		// protobuf omits it on the wire, and the omission decodes back to 0.
		var decoded = NetPacket.DecodePayload<RecipeUnlockMsg>(NetPacket.Encode(NetMsg.RecipeUnlock, new RecipeUnlockMsg { RecipeIndex = 0 }));

		Assert.Equal(0, decoded.RecipeIndex);
	}

	[Fact]
	public void DynamiteExplosion_Position_RoundTrips()
	{
		var decoded = NetPacket.DecodePayload<DynamiteExplosionMsg>(NetPacket.Encode(NetMsg.DynamiteExplosion, new DynamiteExplosionMsg
		{
			ItemInstanceId = 777ul,
			Position = new NetVector2Msg(12.5f, -34.25f),
		}));

		Assert.Equal(777ul, decoded.ItemInstanceId);
		Assert.Equal(12.5f, decoded.Position.X);
		Assert.Equal(-34.25f, decoded.Position.Y);
	}

	[Fact]
	public void RadiationLineState_RoundTripsActiveAndTimeGone()
	{
		var msg = new RadiationLineStateMsg { Active = true, TimeGone = 123.5f };

		var decoded = NetPacket.DecodePayload<RadiationLineStateMsg>(
			NetPacket.Encode(NetMsg.RadiationLineState, msg));

		Assert.True(decoded.Active);
		Assert.Equal(123.5f, decoded.TimeGone);
	}

	[Fact]
	public void BlockDamageSnapshot_OriginZeroDamage_RoundTrips()
	{
		// X/Y are ints and Damage is a float — protobuf's zero omission decodes
		// an omitted 0 back to the SAME 0, so a cell at the origin with zero
		// accumulated damage is the round-trip's boundary case.
		var msg = new BlockDamageSnapshotMsg
		{
			Entries =
			[
				new BlockDamageEntryMsg { X = 0, Y = 0, Damage = 0f },
				new BlockDamageEntryMsg { X = 12, Y = -34, Damage = 73.5f },
			],
		};

		var decoded = NetPacket.DecodePayload<BlockDamageSnapshotMsg>(NetPacket.Encode(NetMsg.BlockDamageSnapshot, msg));

		Assert.Equal(2, decoded.Entries.Count);
		Assert.Equal(0, decoded.Entries[0].X);
		Assert.Equal(0, decoded.Entries[0].Y);
		Assert.Equal(0f, decoded.Entries[0].Damage);
		Assert.Equal(12, decoded.Entries[1].X);
		Assert.Equal(-34, decoded.Entries[1].Y);
		Assert.Equal(73.5f, decoded.Entries[1].Damage);
	}

	[Fact]
	public void BlockDamaged_MetalBonusTrue_RoundTrips()
	{
		// The ×10 metallic-block multiplier depends on this flag crossing the
		// wire — a false omission decodes back to false, a true must survive.
		var decoded = NetPacket.DecodePayload<BlockDamagedMsg>(NetPacket.Encode(NetMsg.BlockDamaged, new BlockDamagedMsg
		{
			Position = new NetVector2Msg(3.5f, -4.25f),
			Damage = 10f,
			MetalBonus = true,
		}));

		Assert.True(decoded.MetalBonus, "MetalBonus=true must round-trip");
	}

	[Fact]
	public void WorldTime_NormalZero_RoundTrips()
	{
		// WorldTimeSpeed.Normal is 0 and omitted by protobuf-net — the omission
		// must decode back to the SAME semantic default (Normal).
		var decoded = NetPacket.DecodePayload<WorldTimeMsg>(NetPacket.Encode(NetMsg.WorldTime, new WorldTimeMsg { Speed = WorldTimeSpeed.Normal }));

		Assert.Equal(WorldTimeSpeed.Normal, decoded.Speed);
	}

	[Fact]
	public void WorldTimeRequest_Fast_RoundTrips()
	{
		var decoded = NetPacket.DecodePayload<WorldTimeRequestMsg>(NetPacket.Encode(NetMsg.WorldTimeRequest, new WorldTimeRequestMsg { Speed = WorldTimeSpeed.Fast }));

		Assert.Equal(WorldTimeSpeed.Fast, decoded.Speed);
	}

	[Fact]
	public void EntitySpawned_CrystalEnemyTint_RoundTrips()
	{
		// The live creation command carries the exact trigger-side post-SetColor
		// color + light intensity (CrystalMimic.cs:32/46) AND the recovery-
		// ownership flag (the enemy domain owns animal recovery); a receiver
		// must see the same values after the wire round-trip — a lost tint
		// would make the fresh copy colorless, a lost flag would re-enter the
		// world-entity recovery tables and resurrect a moved animal.
		var msg = new EntitySpawnedMsg
		{
			Id = "crystalenemy",
			Position = new NetVector2Msg(10f, 20f),
			HasEnemyTint = true,
			EnemyTintColor = new NetColorRgbaMsg(0.25f, 0.5f, 0.75f, 1f),
			EnemyLightIntensity = 0.8f,
			IsAnimal = true,
		};

		var decoded = NetPacket.DecodePayload<EntitySpawnedMsg>(NetPacket.Encode(NetMsg.EntitySpawned, msg));

		Assert.True(decoded.HasEnemyTint);
		Assert.Equal(0.25f, decoded.EnemyTintColor.R);
		Assert.Equal(0.5f, decoded.EnemyTintColor.G);
		Assert.Equal(0.75f, decoded.EnemyTintColor.B);
		Assert.Equal(1f, decoded.EnemyTintColor.A);
		Assert.Equal(0.8f, decoded.EnemyLightIntensity);
		Assert.True(decoded.IsAnimal);
	}

	[Fact]
	public void EntitySpawned_CreationToken_RoundTrips()
	{
		// The creation-instance token is the recovery identity: two identical
		// prefabs created in one cell stay distinct, and a re-report or a
		// snapshot must carry the SAME token end to end — a lost token would
		// collapse them back into one creation.
		var msg = new EntitySpawnedMsg
		{
			Id = "turret",
			Position = new NetVector2Msg(5.2f, 7.2f),
			CreatorSteamId = 76561198000000001ul,
			CreationSequence = 42,
		};

		var decoded = NetPacket.DecodePayload<EntitySpawnedMsg>(NetPacket.Encode(NetMsg.EntitySpawned, msg));

		Assert.Equal(76561198000000001ul, decoded.CreatorSteamId);
		Assert.Equal(42u, decoded.CreationSequence);
		Assert.True(decoded.HasCreationToken);
	}

	[Fact]
	public void RuntimeEntitySnapshot_EntriesAndAnimalKeys_RoundTrip()
	{
		// The snapshot carries the materializable creations AND the animal keys
		// the host accepted for acknowledgement only — a lost animal key would
		// leave the creating guest re-reporting every 60 s, a key mistaken for
		// an entry would materialize a second animal next to the enemy
		// domain's copy.
		var msg = new RuntimeEntitySnapshotMsg
		{
			Entries =
			[
				new EntitySpawnedMsg
				{
					Id = "keypad",
					Position = new NetVector2Msg(1.5f, -2.5f),
					KeypadCode = "4321",
					CreatorSteamId = 2001,
					CreationSequence = 7,
				},
			],
			AcceptedAnimalKeys =
			[
				new RuntimeEntityKeyMsg
				{
					Id = "crystalenemy",
					CellX = 4,
					CellY = -5,
					CreatorSteamId = 2001,
					CreationSequence = 9,
				},
			],
		};

		var decoded = NetPacket.DecodePayload<RuntimeEntitySnapshotMsg>(NetPacket.Encode(NetMsg.RuntimeEntitySnapshot, msg));

		var entry = Assert.Single(decoded.Entries);
		Assert.Equal("keypad", entry.Id);
		Assert.Equal(1.5f, entry.Position.X);
		Assert.Equal(-2.5f, entry.Position.Y);
		Assert.Equal("4321", entry.KeypadCode);
		Assert.Equal(2001ul, entry.CreatorSteamId);
		Assert.Equal(7u, entry.CreationSequence);

		var animalKey = Assert.Single(decoded.AcceptedAnimalKeys);
		Assert.Equal("crystalenemy", animalKey.Id);
		Assert.Equal(4, animalKey.CellX);
		Assert.Equal(-5, animalKey.CellY);
		Assert.Equal(2001ul, animalKey.CreatorSteamId);
		Assert.Equal(9u, animalKey.CreationSequence);
	}

	[Fact]
	public void EnemySpawnEntry_CrystalEnemyTint_RoundTrips()
	{
		// The late-joiner backfill entry mirrors the live EntitySpawned tint; a
		// fresh member materializes the copy and must paint it with the exact
		// host-captured color, not a per-side-random re-roll.
		var msg = new EnemySnapshotMsg
		{
			RuntimeSpawns =
			[
				new EnemySpawnEntryMsg
				{
					Id = new NetworkEntityIdMsg { Epoch = 1, Counter = 2, Generation = 0 },
					PrefabId = "crystalenemy",
					HasTint = true,
					TintColor = new NetColorRgbaMsg(0.1f, 0.2f, 0.3f, 1f),
					LightIntensity = 0.65f,
				},
			],
		};

		var decoded = NetPacket.DecodePayload<EnemySnapshotMsg>(NetPacket.Encode(NetMsg.EnemySnapshot, msg));

		var entry = Assert.Single(decoded.RuntimeSpawns);
		Assert.True(entry.HasTint);
		Assert.Equal(0.1f, entry.TintColor.R);
		Assert.Equal(0.2f, entry.TintColor.G);
		Assert.Equal(0.3f, entry.TintColor.B);
		Assert.Equal(1f, entry.TintColor.A);
		Assert.Equal(0.65f, entry.LightIntensity);
	}

	[Fact]
	public void EnemySpawnEntry_CreationKey_RoundTrips()
	{
		// The backfill identity must survive the wire byte-for-byte: the
		// late-joiner stamps its materialized copy with it, and a surviving live
		// re-report binds by that key (no positional fallback exists any more).
		var msg = new EnemySnapshotMsg
		{
			RuntimeSpawns =
			[
				new EnemySpawnEntryMsg
				{
					Id = new NetworkEntityIdMsg { Epoch = 1, Counter = 2, Generation = 0 },
					PrefabId = "cavetick",
					CreationKey = new RuntimeEntityKeyMsg
					{
						Id = "cavetick",
						CellX = -13,
						CellY = 466,
						CreatorSteamId = 2001,
						CreationSequence = 7,
					},
				},
			],
		};

		var decoded = NetPacket.DecodePayload<EnemySnapshotMsg>(NetPacket.Encode(NetMsg.EnemySnapshot, msg));

		var entry = Assert.Single(decoded.RuntimeSpawns);
		Assert.NotNull(entry.CreationKey);
		Assert.Equal("cavetick", entry.CreationKey.Id);
		Assert.Equal(-13, entry.CreationKey.CellX);
		Assert.Equal(466, entry.CreationKey.CellY);
		Assert.Equal(2001ul, entry.CreationKey.CreatorSteamId);
		Assert.Equal(7u, entry.CreationKey.CreationSequence);
	}

	[Fact]
	public void TrapLayoutEntry_CreationKey_RoundTrips()
	{
		// A runtime-created trap's layout entry carries the host-scanned creation
		// identity so the guest's materialized copy holds it; a generation-time
		// trap carries null.
		var msg = new TrapLayoutSnapshotMsg
		{
			Entries =
			[
				new TrapLayoutEntryMsg
				{
					Kind = EntityEventKind.BearTrapClamped,
					X = 3.5f,
					Y = -9.25f,
					PrefabName = "beartrap",
					CreationKey = new RuntimeEntityKeyMsg
					{
						Id = "beartrap",
						CellX = 3,
						CellY = -10,
						CreatorSteamId = 2002,
						CreationSequence = 11,
					},
				},
				new TrapLayoutEntryMsg { Kind = EntityEventKind.MinePressed, X = 1f, Y = 2f, PrefabName = "mine" },
			],
		};

		var decoded = NetPacket.DecodePayload<TrapLayoutSnapshotMsg>(NetPacket.Encode(NetMsg.TrapLayoutSnapshot, msg));

		Assert.Equal(2, decoded.Entries.Count);
		var runtime = decoded.Entries[0];
		Assert.NotNull(runtime.CreationKey);
		Assert.Equal("beartrap", runtime.CreationKey.Id);
		Assert.Equal(3, runtime.CreationKey.CellX);
		Assert.Equal(-10, runtime.CreationKey.CellY);
		Assert.Equal(2002ul, runtime.CreationKey.CreatorSteamId);
		Assert.Equal(11u, runtime.CreationKey.CreationSequence);
		Assert.Null(decoded.Entries[1].CreationKey);
	}

	[Fact]
	public void CharacterHealth_FaceLatchPresentation_RoundTrips()
	{
		// The 1 Hz character snapshot carries the owner's body-level
		// FacialExpression latches (disfigured/eye loss), the random
		// disfigurement head index and the long-run heal presentation timers
		// so a remote clone can render the same face sprites as its owner.
		var msg = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg
			{
				Disfigured = true,
				EyeGone = true,
				BothEyesGone = true,
				DisfiguredIndex = 2,
				DisfiguredTimeFullSkin = 123.5f,
				EyeTimeHealed = 456.25f,
				HeadMouth = HeadMouthState.Open,
				EatTime = 0.75f,
			},
		};

		var decoded = NetPacket.DecodePayload<CharacterDataMsg>(NetPacket.Encode(NetMsg.CharacterData, msg));

		Assert.True(decoded.Health!.Disfigured);
		Assert.True(decoded.Health.EyeGone);
		Assert.True(decoded.Health.BothEyesGone);
		Assert.Equal(2, decoded.Health.DisfiguredIndex);
		Assert.Equal(123.5f, decoded.Health.DisfiguredTimeFullSkin);
		Assert.Equal(456.25f, decoded.Health.EyeTimeHealed);
		Assert.Equal(HeadMouthState.Open, decoded.Health.HeadMouth);
		Assert.Equal(0.75f, decoded.Health.EatTime);
	}

	[Fact]
	public void CharacterHealth_LegSpeedMult_RoundTrips()
	{
		// The 1 Hz character snapshot carries the owner's computed leg-speed
		// multiplier so the remote render proxy can reproduce the
		// weakness/slouch CrouchAmount input (severe sleepiness postures).
		var msg = new CharacterDataMsg
		{
			Health = new CharacterHealthMsg
			{
				LegSpeedMult = 0.31f,
			},
		};

		var decoded = NetPacket.DecodePayload<CharacterDataMsg>(NetPacket.Encode(NetMsg.CharacterData, msg));

		Assert.Equal(0.31f, decoded.Health!.LegSpeedMult);
	}

}
