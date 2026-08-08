namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The control surfaces a packet handler operates on for one message. Built by
/// SessionService when a direction-valid frame arrives and passed through the
/// router — handlers never depend on the concrete services at construction
/// time (that would create the session ↔ gateway ↔ router ↔ handlers cycle).
/// </summary>
public sealed class HandlerContext(ISessionControl session, IEntitySyncControl entities,
	ICharacterDataControl characterData, IWorldControl world)
{
	public ISessionControl Session { get; } = session;

	public IEntitySyncControl Entities { get; } = entities;

	public ICharacterDataControl CharacterData { get; } = characterData;

	public IWorldControl World { get; } = world;
}
