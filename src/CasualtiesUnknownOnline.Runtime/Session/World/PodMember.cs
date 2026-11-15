using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>One body the drill pod's team gate has to account for.</summary>
/// <param name="SteamId">The owner's stable identity.</param>
/// <param name="Name">The label the host's refusal/log line uses.</param>
/// <param name="InWorld">Whether the session's roster still carries the member in this world.</param>
/// <param name="Alive">The last authoritative aliveness; a dead member never blocks.</param>
/// <param name="Position">The last synced position, or null when no body has reached the host yet.</param>
public readonly record struct PodMember(ulong SteamId, string Name, bool InWorld, bool Alive, NetVector2? Position);
