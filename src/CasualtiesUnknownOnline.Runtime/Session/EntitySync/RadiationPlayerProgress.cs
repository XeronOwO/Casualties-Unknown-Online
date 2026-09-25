namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Engine-agnostic layer-progress fact for the host-side radiation-line
/// straggler rule. The Game Adapter gathers the local + remote entity-stream
/// players; the pure policy below makes the activation decision.
/// </summary>
public readonly struct RadiationPlayerProgress(float y, bool alive)
{
	/// <summary>World-space Y of the player's body.</summary>
	public readonly float Y = y;

	/// <summary>True when the player is alive (dead/left-world players are not stragglers).</summary>
	public readonly bool Alive = alive;
}
