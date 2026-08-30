using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// One in-world player the host-side enemy AI may target: the authoritative
/// stream position plus the local render body when a clone exists for limb
/// resolution. Split out of <see cref="EnemyCombatDirector"/> as the target
/// resolver's data carrier.
/// </summary>
internal sealed class EnemyTarget(ulong steamId, Vector2 position, Body? body)
{
	internal ulong SteamId { get; } = steamId;

	internal Vector2 Position { get; } = position;

	internal Body? Body { get; } = body;

	internal EnemyTargetFact ToFact() => new(SteamId, new NetVector2(Position.x, Position.y));
}
