using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The saved position's layer rule. A character snapshot's position is a coordinate in the layer
/// that captured it: after a layer change the same coordinates describe fresh terrain, and applying
/// them would leave the member away from the group (or inside rock) instead of where its own new
/// layer places it. The capturing client stamps the snapshot with its kernel run baseline; a restore
/// applies the position only while that layer is still this client's.
/// <para>
/// The comparison is made when the position is about to be APPLIED, not when the restore arrives: a
/// reconnecting client receives its saved character before the run-baseline checkpoint (the
/// handshake's own order), so at arrival its current generation is still unknown; by the time the body
/// exists the baseline names the layer the body was generated in.
/// </para>
/// <para>
/// The comparison is the LAYER only. A new run has a fresh character table (the host clears it at the
/// click), and a restored archive keeps the position its own layer names; the run epoch would refuse
/// those legitimate restores, so it is not part of this decision.
/// </para>
/// </summary>
public static class CharacterRestorePositionPolicy
{
	/// <summary>True when both sides name their layer and the snapshot's layer is not the receiver's; an unknown side is never treated as another layer.</summary>
	public static bool IsFromAnotherLayer(WorldGenerationMsg? captured, WorldGenerationMsg? current) =>
		captured is { } from && current is { } now && from.LayerIndex != now.LayerIndex;
}
