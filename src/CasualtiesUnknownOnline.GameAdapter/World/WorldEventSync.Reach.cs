using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The reach question every gesture bound asks: could the reporter's own body have been where it says it acted? It sits
/// in its own part so the world-event file stays inside the class-size cap as bounds are added to it.
/// </summary>
internal sealed partial class WorldEventSync
{
	/// <summary>Could the reporter's body have been at the cell it acted on? An absent position is not a refusal.</summary>
	private bool IsWithinReach(ulong sender, Vector2Int pos)
	{
		if (sender == _session.LocalSteamId)
		{
			return true; // the host's own writes never arrive through the report path
		}

		var entity = _entities.GetRemotePlayer(sender);
		if (entity is null)
		{
			return true; // no position for this peer yet
		}

		var dx = entity.Position.X - pos.x;
		var dy = entity.Position.Y - pos.y;
		return Mathf.Sqrt((dx * dx) + (dy * dy)) <= InteractionReach.Cells;
	}
}
