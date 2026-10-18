using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The fixed-step driver on a host clone's <c>Body</c> GameObject. It exists only on the host and
/// only on a render clone of a remote player — the marker <see cref="HostVitalsBodies.IsSimulated"/>
/// reads — and it runs the native vitals pass from <c>FixedUpdate</c>, where Unity reports the
/// fixed step as <c>Time.deltaTime</c>. The clone's own <c>Body.FixedUpdate</c> is skipped by the
/// render-proxy patch, so this component is the one clock the model has.
///
/// <para>
/// The mutable state lives here rather than in the domain: it belongs to this body and dies with
/// it. The domain owns the dependencies (session, character table, restore applier) and the rules.
/// </para>
/// </summary>
internal sealed class HostVitalsDriver : MonoBehaviour
{
	internal HostVitalsBodies Bodies = null!;

	internal Body Body = null!;

	internal ulong SteamId { get; private set; }

	/// <summary>The snapshot was mapped onto this body once; the model may advance.</summary>
	internal bool Seeded;

	/// <summary>The snapshot object the inputs were last applied from — a new report or an authoritative save is a new object.</summary>
	internal CharacterDataMsg? LastApplied;

	/// <summary>The snapshot's local mutation counter at the last input apply: some event appliers mutate the stored object in place.</summary>
	internal int LastAppliedRevision;

	/// <summary>The client-owned fields the host pass must not persist (see <see cref="HostVitalsQuarantine"/>).</summary>
	internal readonly HostVitalsQuarantine Quarantine = new();

	/// <summary>The owned values this body last wrote back into the stored snapshot, indexed by limb index for the limb part.</summary>
	internal float[] LastWrittenBleeds = [];

	internal float LastWrittenBloodVolume;

	internal float LastWrittenTotalBleedSpeed;

	/// <summary>One-time diagnostics: the fixed-step assumption, a skipped stall, a stored non-finite value.</summary>
	internal bool LoggedFixedStep;

	internal bool WarnedStalledStep;

	internal bool WarnedNonFiniteSnapshot;

	/// <summary>One-time diagnostic: the owner reports climbing but this host's world resolved no climbable for the clone.</summary>
	internal bool WarnedClimbUnresolved;

	/// <summary>The model inputs applied at the last pass, so a CHANGE is one Debug line instead of per-step noise.</summary>
	internal HostVitalsInputs LastInputs;

	internal void Initialize(HostVitalsBodies bodies, Body body, ulong steamId)
	{
		Bodies = bodies;
		Body = body;
		SteamId = steamId;
		LastWrittenBleeds = new float[body.limbs.Length];
	}

	private void FixedUpdate() => Bodies.Step(this);
}
