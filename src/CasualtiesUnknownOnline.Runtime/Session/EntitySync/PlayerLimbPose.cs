using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// One player limb's continuous render-pose fact for the high-frequency state
/// stream. The local body's ragdoll/dead/unconscious limbs are physics-driven;
/// a frozen render proxy cannot replicate that physics, so the owner publishes
/// each visible limb's world-space transform (position + z rotation) and the
/// peer writes it directly onto the clone's corresponding limb. World-space is
/// required because the visible limbs are not reliably centered on the Body
/// transform — the proven ragdoll sync path (KrokMP) writes rigidbody world
/// positions, not local offsets.
/// </summary>
public sealed class PlayerLimbPose
{
	public int Index { get; set; }

	public NetVector2 WorldPosition { get; set; }

	/// <summary>World-space Z euler angle in degrees.</summary>
	public float RotationZ { get; set; }
}
