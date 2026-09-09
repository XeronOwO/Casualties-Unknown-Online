using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// One unacknowledged runtime entity creation: the exact creation message the
/// guest reported plus how many fallback windows have re-sent it (the stall
/// warning's counter — the entry is never dropped for age; only the host's
/// answer, the entity's death or a world/session boundary ends it).
/// </summary>
public sealed record PendingEntityReport(EntitySpawnedMsg Msg, int Attempts);
