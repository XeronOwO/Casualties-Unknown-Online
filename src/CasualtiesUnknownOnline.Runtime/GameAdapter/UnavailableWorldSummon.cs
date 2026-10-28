using CasualtiesUnknownOnline.Runtime.Data;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The default <see cref="IWorldSummon"/> in a build with no Game Adapter: there is no prefab table and no
/// creation call, so every answer is the fact, not a silent success.
/// </summary>
internal sealed class UnavailableWorldSummon : IWorldSummon
{
	private const string Ground = "this build has no Game Adapter, so it cannot create world objects";

	public bool TryResolveAnchor(WorldSummonAnchor anchor, out float x, out float y, out string? refusal)
	{
		x = 0f;
		y = 0f;
		refusal = $"No position can be resolved: {Ground}.";
		return false;
	}

	public WorldSummonOutcome TrySummon(string prefabId, WorldSummonKind kind, SnbtValue? state, float x, float y) =>
		new(false, 0, "", new GrantRefusal($"No prefab can be created: {Ground}.", "summon.answer.refused_no_adapter"));
}
