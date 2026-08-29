using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Immutable player terminal-fact table owned by the kernel. Reducers produce
/// new snapshots so the kernel can swap atomically after invariant validation.
/// </summary>
public sealed record PlayerStateTable(IReadOnlyList<PlayerState> Players)
{
	public static readonly PlayerStateTable Empty = new([]);

	public PlayerStateTable Upsert(PlayerState state) =>
		this with
		{
			Players = [.. Players.Where(p => p.SteamId != state.SteamId), state],
		};
}
