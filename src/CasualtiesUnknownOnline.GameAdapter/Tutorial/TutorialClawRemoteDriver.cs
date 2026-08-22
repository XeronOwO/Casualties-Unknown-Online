using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Tutorial;

/// <summary>
/// Guest-side presentation helper for the host-authoritative tutorial-claw
/// stream. The host's TutorialHandler is the live rig; a guest that is NOT
/// running its own tutorial course drives the same claw-arm visual from the
/// 20 Hz stream. This component only overrides the claw-arm material in
/// LateUpdate (after TutorialHandler.Update's own material selection) and
/// stops overriding as soon as a local course is active — per-side course
/// state remains the owner of the claw in that case.
/// </summary>
internal sealed class TutorialClawRemoteDriver : MonoBehaviour
{
	private byte _material = TutorialClawStateMsg.MaterialOpen;

	internal void Apply(byte material) => _material = material;

	private void LateUpdate()
	{
		var tutorial = TutorialHandler.main;
		if (tutorial == null || tutorial.activeCourse != null || tutorial.clawArm == null) // Unity objects — ==
		{
			return;
		}

		tutorial.clawArm.lineRenderer.material = _material switch
		{
			TutorialClawStateMsg.MaterialClosed => tutorial.clawArmClosed,
			TutorialClawStateMsg.MaterialPlace => tutorial.clawArmPlace,
			TutorialClawStateMsg.MaterialKnife => tutorial.clawArmKnife,
			_ => tutorial.clawArmOpen,
		};
	}
}
