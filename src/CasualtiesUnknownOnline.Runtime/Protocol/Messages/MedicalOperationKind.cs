namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The category of a remote medical operation. Stage 1 implements injection;
/// Stage 2 adds the shared shrapnel session; Stage 3 adds the remaining native
/// medical minigames/actions. The session envelope and the terminal/state
/// messages are shared; only the payload shape differs.
/// </summary>
public enum MedicalOperationKind : int
{
	/// <summary>
	/// No kind at all — the wire default, and NEVER a value a sender means: every message
	/// builder names its own. The sentinel exists because the plausible alternative, a default
	/// of <see cref="Injection"/>, turns an omitted field into a silent WRONG BRANCH on the
	/// receiving client: 2026-10-11 found the shared shrapnel session's state messages omitting
	/// the field, being read as injections, and therefore dropping the authority's own limb
	/// write without a single log line — the very write the session existed to commit.
	/// </summary>
	None = 0,

	Injection = 1,
	Shrapnel = 2,
	Bandage = 3,
	SplintRemoval = 4,
	TourniquetRemoval = 5,
	Dislocation = 6,
	Aed = 7,
	ManualDefib = 8,
	Amputation = 9,

	/// <summary>
	/// Cardiopulmonary resuscitation: a bare-hand operation on a patient in cardiac arrest. Its update action is
	/// <see cref="MedicalOperationUpdateAction.Compress"/>, whose value is the operator's measured interval between
	/// presses, and the pressure it writes is the rule ticket `remote-medical-cpr.md` records (compressions act on blood
	/// pressure; ventilation on oxygen; the restart is one outcome the host arbitrates).
	/// </summary>
	Cpr = 10,
}
