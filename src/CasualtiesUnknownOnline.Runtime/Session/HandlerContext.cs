using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The control surfaces a packet handler operates on for one message. Built by
/// SessionService when a direction-valid frame arrives and passed through the
/// router — handlers never depend on the concrete services at construction
/// time (that would create the session ↔ gateway ↔ router ↔ handlers cycle).
/// </summary>
public sealed class HandlerContext(ISessionControl session, IEntitySyncControl entities,
	ICharacterDataControl characterData, IWorldControl world, IItemControl items, IModsControl mods,
	ICraftControl craft, IEnemySyncControl enemies, IWorldTimeControl worldTime,
	IPlayerInteractionControl playerInteraction, ITutorialClawControl tutorialClaw)
{
	public ISessionControl Session { get; } = session;

	public IEntitySyncControl Entities { get; } = entities;

	public ICharacterDataControl CharacterData { get; } = characterData;

	public IWorldControl World { get; } = world;

	/// <summary>The world-time domain (host-authoritative speed requests/broadcasts).</summary>
	public IWorldTimeControl WorldTime { get; } = worldTime;

	/// <summary>The direct player-interaction domain (take items from another player, host-authoritative).</summary>
	public IPlayerInteractionControl PlayerInteraction { get; } = playerInteraction;

	/// <summary>The tutorial-claw presentation stream (host-authoritative 20 Hz claw visual).</summary>
	public ITutorialClawControl TutorialClaw { get; } = tutorialClaw;

	public IItemControl Items { get; } = items;

	/// <summary>The mod domain (Phase 4 Mod API): message routing + the discovery state the handshake check reads.</summary>
	public IModsControl Mods { get; } = mods;

	/// <summary>The crafting domain: the one-operation-one-report apply + the recipe-unlock surface.</summary>
	public ICraftControl Craft { get; } = craft;

	/// <summary>The enemy-sync domain (host-authoritative enemy snapshots).</summary>
	public IEnemySyncControl Enemies { get; } = enemies;

	/// <summary>
	/// Host side: hand one member the full world-state snapshots — the
	/// world-entry fan-out. SceneStateHandler's InWorld edge and the reconnect
	/// handshake (a member still InWorld) both call this: the handshake
	/// restores <c>member.InWorld</c> from the peer's scene report and thus
	/// never fires the edge, so the snapshots must fan out there too.
	/// Each snapshot is a late-joiner/reconnect backfill:
	/// - block-state: the damage table, so the member sees the world as it is
	///   now, not as the baseline regenerated it;
	/// - trap-state: one-shot trap consumptions (used to ride only the 60 s
	///   periodic resend — a rejoin saw spent traps fire up to a minute late);
	/// - opened-entities: one-shot opens (an open has no re-open — a rejoin
	///   must learn them from the host; observed: the pod door closed again);
	/// - building-entity health: current damage/destroy state (a rejoin would
	///   otherwise regenerate destroyed plants/crates at full health);
	/// - block damage: current accumulated BlockDamage.damage (a rejoin would
	///   otherwise regenerate partially-mined blocks at full HP and break them
	///   later — the live delta chain only covers members that were present);
	/// - trap-layout: the generated trap positions (the entity distribution
	///   runs physics queries the random isolation does not cover, so the
	///   member's layout diverges — the host's scene is the authority);
	/// - world-items: runtime drops and placed items the member could not
	///   have regenerated;
	/// - enemies: the host's authoritative enemy ids + presentation state (the
	///   member binds its locally generated enemy copies to the host's ids).
	/// </summary>
	public void SendWorldStateToMember(ulong steamId)
	{
		World.SendBlockStateSnapshot(steamId);
		World.SendBlockDamageSnapshot(steamId);
		World.SendTrapStateSnapshot(steamId);
		World.SendOpenedEntitiesSnapshot(steamId);
		World.SendBuildingEntityHealthSnapshot(steamId);
		World.SendTrapLayoutSnapshot(steamId);
		Items.SendItemSnapshot(steamId);
		Enemies.SendEnemySnapshot(steamId);
	}
}
