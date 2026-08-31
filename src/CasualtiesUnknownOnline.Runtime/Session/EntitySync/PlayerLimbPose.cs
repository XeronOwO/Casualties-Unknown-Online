using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// One player limb's continuous render-pose fact for the high-frequency state
/// stream. The local body's ragdoll/dead/unconscious limbs are physics-driven;
/// a frozen render proxy cannot replicate that physics, so the owner publishes
/// each visible limb's local transform (position + z rotation) and the peer
/// writes it onto the clone's corresponding limb.
/// </summary>
public sealed class PlayerLimbPose
{
	public int Index { get; set; }

	public NetVector2 LocalPosition { get; set; }

	/// <summary>Z euler angle in degrees.</summary>
	public float RotationZ { get; set; }
}
