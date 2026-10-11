using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The item-instance-id coordination: the per-guest counter watermarks (a
/// crashed-and-rejoined guest's counter restarts from zero and would reuse ids
/// the host's tables still hold — the host records the high-water mark and
/// grants it back on rejoin) and the carried-inventory registration (the
/// guest's self-assigned starting-supply ids, registered in the transfer table
/// so its use/slot reports arbitrate normally).
///
/// The same rule has a second source: a restored cut re-stamps the ARCHIVE's
/// ids onto the objects this process rebuilds, and an id is (counter, id space)
/// — the space being the account id's LOW 32 BITS — so the archive's own rows
/// name the resume point of every id space that played the world, the local one
/// included (its counter also restarts with the process). The restore-driven
/// half is driven the way the game drives it: one "process" plays a world and
/// cuts it, a SECOND one opens the same archive and continues the world, which
/// is the only driver under which the per-process counters really restart. It
/// uses account-sized ids: with ids that fit in 32 bits, "the id space" and "the
/// full account id" are the same number and the defect this suite pins is
/// invisible.
///
/// The archive carries those ids in TWO places, and the rule reads both: the
/// checkpoint's item rows (world, carried, contained and worn — what the kernel
/// holds) and <c>characters/&lt;playerKey&gt;.json</c> (what the live 1 Hz report
/// wrote, which the kernel may never have learned). The last two cases are the
/// ones that pin the second carrier and the synthesized namespace.
/// </summary>
[Trait("Category", "Integration")]
public class ItemIdCoordinatorTests
{
	[Fact]
	public void GuestWatermark_HighWaterSurvives_RegrantedOnRejoin()
	{
		using var w = ItemSimWorld.Create();

		// The guest allocated up to counter 42, then reported a stale 10 (an
		// out-of-order retransmit) — the high-water mark must survive.
		w.G1.Services.GetRequiredService<IItemControl>().SendItemIdWatermark(42);
		w.Driver.Tick(50);
		w.G1.Services.GetRequiredService<IItemControl>().SendItemIdWatermark(10);
		w.Driver.Tick(50);

		// The guest vanishes from the lobby — the presence check removes it
		// from the host's session after ~2 s (SessionSimulationTests precedent).
		w.Host.Steam.LobbyMembers = [w.Host.SteamId, w.G2.SteamId];
		w.G1.Steam.LobbyMembers = [w.G1.SteamId];
		w.Driver.Tick(2100);
		Assert.False(w.G1.Session.SessionActive, "the guest's session must end when the lobby loses the host");

		// The guest returns: the lobby membership restores, the rejoin flow
		// fires the lobby-entered callback again, the handshake rebuilds —
		// the host grants the RECORDED high-water mark (42), not the stale 10.
		w.Host.Steam.LobbyMembers = [w.Host.SteamId, w.G1.SteamId, w.G2.SteamId];
		w.G1.Steam.LobbyMembers = [w.Host.SteamId, w.G1.SteamId, w.G2.SteamId];
		w.G1.Steam.FireLobbyEntered(9001);
		w.Driver.TickUntil(() => w.Host.Session.Members.Count(m => m.Handshaken) == 2, maxMs: 5000);

		// The rejoin's grant rides the wire (the initial handshake's grant was
		// before the recording surface went up — the list starts clean).
		var grants = w.Watermarks(w.G1);
		Assert.True(grants.Count == 1, $"the rejoin must grant the recorded watermark once, got {grants.Count}");
		Assert.True(grants[0].Counter == 42, $"the grant must carry the high-water mark (42), got {grants[0].Counter}");
	}

	[Fact]
	public void CarriedInventory_RegistersInTheTransferTable()
	{
		using var w = ItemSimWorld.Create();

		w.G1.Services.GetRequiredService<IItemControl>().SendCarriedInventory(
		[
			new CharacterItemMsg { InstanceId = 101, ItemId = "ore", Condition = 1f },
			new CharacterItemMsg { InstanceId = 102, ItemId = "medkit", Condition = 0.5f },
		]);
		w.Driver.Tick(50);

		Assert.True(w.TransferredOf(w.G1, 101), "the first starting supply registered in the transfer table");
		Assert.True(w.TransferredOf(w.G1, 102), "the second starting supply registered in the transfer table");
	}

	[Fact]
	public void AContinuedWorld_ResumesTheLocalAllocator_AboveTheIdsTheArchiveReissued()
	{
		var saves = NewSavesRoot();
		using (var previous = Process(saves))
		{
			// The previous process handed the local id space counters 0 and 7, a guest's space
			// its own 9, and the synthesized namespace a 12: the archive carries all four rows,
			// and a restore re-stamps them onto the objects this process rebuilds while its own
			// counters start at zero.
			Assert.True(Spawn(previous, HostAccount, InstanceId(0, HostAccount), 5f));
			Assert.True(Spawn(previous, HostAccount, InstanceId(7, HostAccount), 6f));
			Assert.True(Spawn(previous, Guest1Account, InstanceId(9, Guest1Account), 7f));
			Assert.True(Spawn(previous, HostAccount, AwardedId(12UL, HostAccount), 8f));
			CutTheWorld(previous);
		}

		using var restarted = Process(saves);
		var resumes = new List<ulong>();
		restarted.Host.Services.GetRequiredService<IItemControl>().ItemIdWatermarkReceived += resumes.Add;

		Assert.True(Save(restarted).TryContinue(out var outcome), outcome.Summary);

		// The local resume point is the LOCAL space's high-water mark (7) — not the archive's
		// overall maximum (9, another account's space), and not the synthesized namespace's 12:
		// that is a different counter, and reading it as this one would push the allocator into
		// the namespace whose ids carry the high bit.
		Assert.Equal([7UL], resumes);
	}

	[Fact]
	public void AContinuedWorld_RaisesTheWatermarkOfAMemberAlreadyInTheSession()
	{
		var saves = NewSavesRoot();
		using (var previous = Process(saves))
		{
			// The archive holds a guest-owned id at counter 9 (that guest's previous process)
			// and the host's own at counter 3; the guest is in the lobby while the host
			// continues the world.
			Assert.True(Spawn(previous, Guest1Account, InstanceId(9, Guest1Account), 5f));
			Assert.True(Spawn(previous, HostAccount, InstanceId(3, HostAccount), 6f));
			CutTheWorld(previous);
		}

		using var restarted = Process(saves);
		Assert.True(Save(restarted).TryContinue(out var outcome), outcome.Summary);

		// The character this guest is about to receive carries those ids, so the raised
		// resume point has to reach it now — a present member sees no second handshake.
		// The other member's space holds nothing from the archive, so it is left alone.
		Assert.Equal(9UL, Assert.Single(restarted.Watermarks(restarted.G1)).Counter);
		Assert.Empty(restarted.Watermarks(restarted.G2));
	}

	[Fact]
	public void AContinuedWorld_KeepsAnAbsentMembersRestoredMark_ForItsNextHandshake()
	{
		var saves = NewSavesRoot();
		using (var previous = Process(saves))
		{
			Assert.True(Spawn(previous, Guest2Account, InstanceId(9, Guest2Account), 5f));
			CutTheWorld(previous);
		}

		using var restarted = Process(saves);

		// The guest that owns that space leaves the lobby before the host continues.
		restarted.Host.Steam.LobbyMembers = [restarted.Host.SteamId, restarted.G1.SteamId];
		restarted.G2.Steam.LobbyMembers = [restarted.G2.SteamId];
		restarted.Driver.Tick(2100);
		Assert.False(restarted.G2.Session.SessionActive, "the guest's session must end when the lobby loses the host");

		Assert.True(Save(restarted).TryContinue(out var outcome), outcome.Summary);

		// Nothing to grant: the member is not here. The mark is KEPT for its handshake.
		Assert.Empty(restarted.Watermarks(restarted.G2));

		// A session boundary is not a world boundary: the mark the archive put in belongs to
		// the world those ids live in — a host may continue the same world in a new lobby —
		// so the reset that ends a session must not forget it. Forgetting it is how a member
		// whose process restarted is granted 0 and hands out ids the world already holds.
		restarted.Items.ResetSessionState();

		// The guest returns: the grant must carry the restored high-water mark (9), not 0.
		restarted.Host.Steam.LobbyMembers = [restarted.Host.SteamId, restarted.G1.SteamId, restarted.G2.SteamId];
		restarted.G2.Steam.LobbyMembers = [restarted.Host.SteamId, restarted.G1.SteamId, restarted.G2.SteamId];
		restarted.G2.Steam.FireLobbyEntered(9001);
		restarted.Driver.TickUntil(() => restarted.Host.Session.Members.Count(m => m.Handshaken) == 2, maxMs: 5000);

		var rejoined = restarted.Watermarks(restarted.G2);
		Assert.Equal(9UL, rejoined[rejoined.Count - 1].Counter);
	}

	[Fact]
	public void AContinuedWorld_ResumesTheSynthesizedNamespace_AboveTheAwardedIdsTheArchiveReissued()
	{
		var saves = NewSavesRoot();
		var awarded = AwardedId(5UL, HostAccount);
		using (var previous = Process(saves))
		{
			// The previous process's last removal awarded the operator a splint. The awarded
			// item is a real carried item, so the cut carries it in BOTH of its id carriers:
			// the kernel's item rows and the operator's character file.
			Characters(previous.Host).SaveHostCharacterData(SplintedBody(HostAccount, awarded));
			Characters(previous.Host).SaveCharacterData(Guest1Account, HealthyBody(Guest1Account));
			Assert.True(Kernel(previous).TrySpawn(
				HostAccount,
				new ItemIdentity(awarded, "splint"),
				ItemLocation.Carried(new ActorId(HostAccount)),
				new CharacterItemMsg { InstanceId = awarded, ItemId = "splint", Condition = 0.75f },
				out _,
				out _));
			CutTheWorld(previous);
		}

		using var restarted = Process(saves);
		Assert.True(Save(restarted).TryContinue(out var outcome), outcome.Summary);
		MarkInWorld(restarted.Host);
		MarkInWorld(restarted.G1);

		// The restored world already holds that awarded id and the operator's restored body
		// still wears the splint, so the next removal mints the next id of the synthesized
		// namespace. That counter is per process exactly like the plain one, so without a
		// resume the removal restarts it from `AwardedHighBit | 0 | space`: the archive's own
		// awarded id is at counter 5, and 0 is not above it — which is the rule this case
		// pins. (Whether the re-issued counter is ALSO a live id depends on what the archive
		// holds; the real-machine probe of the parent ticket is the run that showed the
		// collision itself, because that archive's lowest id was counter 0.)
		var end = RemoveTheSplint(restarted);
		var minted = end.AwardedItem!.InstanceId;
		Assert.True(ItemInstanceIds.IsAwarded(minted), $"the removal must mint from the synthesized namespace, got {minted}");
		Assert.True(
			minted != awarded,
			"the continuation re-issued the awarded id the restored world already holds (one id, two objects)");
		Assert.True(
			AwardedCounterOf(minted) > AwardedCounterOf(awarded),
			$"the resumed counter must be above the archive's own ({AwardedCounterOf(awarded)}), got {AwardedCounterOf(minted)} ({minted})");
	}

	[Fact]
	public void AContinuedWorld_IgnoresAnotherAccountsAwardedIds_WhenResumingTheSynthesizedCounter()
	{
		var saves = NewSavesRoot();
		using (var previous = Process(saves))
		{
			// The world was last played by ANOTHER host account, whose awards live in its own id
			// space. This process mints in ITS space, so those ids can never collide with what it
			// awards — and resuming from them would be the same mistake the plain half already
			// pins, one namespace over: another space's maximum is not this counter's resume point.
			Assert.True(Spawn(previous, Guest1Account, AwardedId(9UL, Guest1Account), 5f));
			CutTheWorld(previous);
		}

		using var restarted = Process(saves);
		Assert.True(Save(restarted).TryContinue(out var outcome), outcome.Summary);
		MarkInWorld(restarted.Host);
		MarkInWorld(restarted.G1);
		Characters(restarted.Host).SaveHostCharacterData(SplintedBody(HostAccount, InstanceId(0, HostAccount)));
		Characters(restarted.Host).SaveCharacterData(Guest1Account, HealthyBody(Guest1Account));

		var minted = RemoveTheSplint(restarted).AwardedItem!.InstanceId;
		Assert.True(ItemInstanceIds.IsAwarded(minted), $"the removal must mint from the synthesized namespace, got {minted}");
		Assert.Equal(0UL, AwardedCounterOf(minted));
		Assert.Equal(ItemInstanceIds.SpaceOf(HostAccount), ItemInstanceIds.OwnerOf(minted));
	}

	[Fact]
	public void AContinuedWorld_SeedsAGuestSpaceFromItsCharacterFile_WhenTheKernelNeverLearnedTheId()
	{
		var saves = NewSavesRoot();
		using (var previous = Process(saves))
		{
			// The guest's 1 Hz report carries its self-assigned ids and the kernel never
			// learned them: the carried registration is a separate, slower cadence, so inside
			// that window the character file is the id's ONLY carrier.
			previous.G1.Services.GetRequiredService<ICharacterDataControl>().ReportCharacterData(
				HealthyBody(Guest1Account, new CharacterItemMsg
				{
					InstanceId = InstanceId(12, Guest1Account),
					ItemId = "rope",
					Condition = 1f,
					Contents =
					[
						new CharacterItemMsg { InstanceId = InstanceId(13, Guest1Account), ItemId = "water", Condition = 0.5f },
					],
				}));
			previous.Driver.Tick(50);
			Assert.Empty(Kernel(previous).QueryItems()); // the window this case is about
			CutTheWorld(previous);
		}

		using var restarted = Process(saves);
		Assert.True(Save(restarted).TryContinue(out var outcome), outcome.Summary);

		// The restored character comes back wearing those ids, so the guest's counter must
		// resume above them — including the id that only the nested content carries.
		Assert.Equal(13UL, Assert.Single(restarted.Watermarks(restarted.G1)).Counter);
	}

	/// <summary>
	/// The format's own contract, because two mechanisms now read it: the space key is the
	/// account id's low 32 bits (never the account id itself — that is the comparison a
	/// resume gets wrong), an id hands that key back as its owner half, and the synthesized
	/// namespace is marked so a plain resume can tell it apart.
	/// </summary>
	[Fact]
	public void TheIdSpaceKey_IsTheAccountIdsLowBits_AndTheSynthesizedNamespaceIsMarked()
	{
		Assert.Equal(HostAccount & 0xFFFFFFFFUL, ItemInstanceIds.SpaceOf(HostAccount));
		Assert.NotEqual(HostAccount, ItemInstanceIds.SpaceOf(HostAccount));

		var id = ItemInstanceIds.Compose(7, HostAccount);
		Assert.Equal(7UL, ItemInstanceIds.CounterOf(id));
		Assert.Equal(ItemInstanceIds.SpaceOf(HostAccount), ItemInstanceIds.OwnerOf(id));
		Assert.False(ItemInstanceIds.IsAwarded(id));

		Assert.True(ItemInstanceIds.IsAwarded(ItemInstanceIds.AwardedHighBit | ItemInstanceIds.SpaceOf(HostAccount)));
	}

	// ---- the fixture: two processes over one world archive ----

	/// <summary>Account-sized ids: an id's owner half is its account id's LOW 32 bits, so a world whose ids fit in 32 bits cannot tell the two apart — the defect this suite pins hid behind exactly that.</summary>
	private const ulong HostAccount = 76561198000001001UL;

	private const ulong Guest1Account = 76561198000002001UL;

	private const ulong Guest2Account = 76561198000003001UL;

	/// <summary>
	/// One process over a world archive root: the production composition root, the trio
	/// handshaken, the save system enabled. Two of these over ONE root are the claim's
	/// scenario — the first plays and cuts, the second relaunches and continues — and the
	/// counters really do restart between them, which no single-process driver can show.
	/// </summary>
	private static ItemSimWorld Process(string savesRoot) => ItemSimWorld.Create(
		hostSteamId: HostAccount,
		guest1SteamId: Guest1Account,
		guest2SteamId: Guest2Account,
		savesRoot: savesRoot);

	/// <summary>A fresh archive root — the world the following processes share.</summary>
	private static string NewSavesRoot() =>
		Path.Combine(Path.GetTempPath(), "cuo-id-resume-tests", Guid.NewGuid().ToString("N"), "cuo", "saves");

	private static IWorldSaveControl Save(ItemSimWorld world) =>
		world.Host.Services.GetRequiredService<IWorldSaveControl>();

	private static ICharacterDataControl Characters(TestNode node) =>
		node.Services.GetRequiredService<ICharacterDataControl>();

	/// <summary>The host's kernel — what a cut carries and what a restore re-issues.</summary>
	private static ItemKernelAuthority Kernel(ItemSimWorld world) =>
		world.Host.Services.GetRequiredService<ItemKernelAuthority>();

	/// <summary>Play one mid-run cut of the world the previous process is playing: a run baseline, one armed cut, taken at the frame-end seam.</summary>
	private static void CutTheWorld(ItemSimWorld world)
	{
		Assert.True(Save(world).TryBeginRun(isTutorial: false), "the fixture's world could not be created");
		Assert.True(Kernel(world).TryStartRun(world.Host.SteamId, WorldSaveCaptureTests.Run(layerIndex: 2), out _, out _));
		Assert.True(Save(world).TryRequestCut(WorldCutReason.MenuReturn, out var refusal), refusal);
		Assert.NotNull(Save(world).TryCaptureArmedCut(null, frame: 0));
	}

	private static void MarkInWorld(TestNode node) =>
		node.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

	/// <summary>One restored item row: the kernel table is what a restore re-issues ids from.</summary>
	private static bool Spawn(ItemSimWorld world, ulong actor, ulong instanceId, float x) =>
		Kernel(world).TrySpawn(actor, new ItemIdentity(instanceId, "shell"), ItemLocation.World(x, x), Shell(), out _, out _);

	private static CharacterItemMsg Shell() => new() { ItemId = "shell" };

	/// <summary>
	/// The synthesized namespace's last removal, driven through the operation the player
	/// performs it with: the guest operates on the host's splinted limb and the removed
	/// splint is awarded to the operator — the one production consumer of that counter.
	/// </summary>
	private static MedicalOperationEndCommittedMsg RemoveTheSplint(ItemSimWorld world)
	{
		var operations = world.G1.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		MedicalOperationStartAckMsg? ack = null;
		MedicalOperationEndCommittedMsg? end = null;
		operations.StartAckReceived += msg => ack = msg;
		operations.EndCommittedReceived += msg => end = msg;

		operations.SendOtherStartRequest(world.Host.SteamId, 0, 1, MedicalOperationKind.SplintRemoval);
		Assert.True(ack is { Accepted: true }, $"the removal of the restored splint was refused: {ack?.RejectReason}");
		operations.SendOtherEndRequest(ack!.OperationId, 0f);

		Assert.NotNull(end);
		Assert.NotNull(end!.AwardedItem);
		return end;
	}

	/// <summary>An operator body that can operate: alive, conscious, one splinted limb and whatever the case adds.</summary>
	private static CharacterDataMsg SplintedBody(ulong owner, ulong awardedItemId) => new()
	{
		OwnerSteamId = owner,
		Items = [new CharacterItemMsg { InstanceId = awardedItemId, ItemId = "splint", Condition = 0.75f, SlotIndex = 0 }],
		Health = new CharacterHealthMsg { Alive = true, Conscious = true, BrainHealth = 80f },
		Limbs =
		[
			new CharacterLimbMsg { Index = 0 },
			new CharacterLimbMsg
			{
				Index = 1,
				SkinHealth = 50f,
				MuscleHealth = 50f,
				DistanceToHeart = 2,
				ConnectedLimbIndices = [2],
				Components =
				[
					new ComponentStateMsg
					{
						TypeName = "SplintLimb",
						Fields =
						[
							new ComponentFieldMsg { Name = "condition", Kind = SaveableFieldKind.Float, FloatValue = 0.75f },
							new ComponentFieldMsg { Name = "item", Kind = SaveableFieldKind.String, StringValue = "splint" },
						],
					},
				],
			},
			new CharacterLimbMsg { Index = 2, DistanceToHeart = 4 },
		],
	};

	/// <summary>A body that is simply alive and conscious — what the operator-side and report-side checks read.</summary>
	private static CharacterDataMsg HealthyBody(ulong owner, params CharacterItemMsg[] items) => new()
	{
		OwnerSteamId = owner,
		Items = [.. items],
		Health = new CharacterHealthMsg { Alive = true, Conscious = true, BrainHealth = 80f },
	};

	/// <summary>The synthesized namespace's own counter, stated here independently of the production helper: the high bit is not part of it.</summary>
	private static ulong AwardedCounterOf(ulong awardedInstanceId) => (awardedInstanceId & ~ItemInstanceIds.AwardedHighBit) >> 32;

	private static ulong AwardedId(ulong counter, ulong accountId) =>
		ItemInstanceIds.AwardedHighBit | (counter << 32) | ItemInstanceIds.SpaceOf(accountId);

	/// <summary>The id format the claim is about, stated here independently of the production helper: (counter, account-low).</summary>
	private static ulong InstanceId(ulong counter, ulong accountId) => (counter << 32) | (uint)accountId;
}
