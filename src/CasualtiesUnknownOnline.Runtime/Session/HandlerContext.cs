using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The control surfaces a packet handler operates on for one message. Built by
/// SessionService when a direction-valid frame arrives and passed through the
/// router — handlers never depend on the concrete services at construction
/// time (that would create the session ↔ gateway ↔ router ↔ handlers cycle).
/// </summary>
public sealed class HandlerContext(ISessionControl session, IEntitySyncControl entities,
	ICharacterDataControl characterData, IWorldControl world, IItemControl items, IModsControl mods)
{
	public ISessionControl Session { get; } = session;

	public IEntitySyncControl Entities { get; } = entities;

	public ICharacterDataControl CharacterData { get; } = characterData;

	public IWorldControl World { get; } = world;

	public IItemControl Items { get; } = items;

	/// <summary>The mod domain (Phase 4 Mod API): message routing + the discovery state the handshake check reads.</summary>
	public IModsControl Mods { get; } = mods;
}
