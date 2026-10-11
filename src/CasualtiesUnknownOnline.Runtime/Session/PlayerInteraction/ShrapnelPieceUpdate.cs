namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One operator→host shrapnel piece report. It is the API-side payload for the
/// shared shrapnel session; the wire message remains the generic
/// <c>MedicalOperationUpdateMsg</c> with the shrapnel-specific fields filled.
/// </summary>
public sealed class ShrapnelPieceUpdate
{
	/// <summary>The native minigame slot index (0-4).</summary>
	public int PieceIndex { get; set; } = -1;

	/// <summary>Authoritative minigame-space X.</summary>
	public float X { get; set; }

	/// <summary>Authoritative minigame-space Y.</summary>
	public float Y { get; set; }

	/// <summary>True when this report is the operator grabbing the piece.</summary>
	public bool Grabbed { get; set; }

	/// <summary>True when this report releases an owned piece without removing it.</summary>
	public bool Released { get; set; }

	/// <summary>True when the native break-grasp failure occurred on this piece.</summary>
	public bool BreakGrasp { get; set; }

	/// <summary>
	/// The reporting client's own counter for the grip slips it has drawn in this session, 1-based; 0
	/// means that client held no derived window and drew the native values from the free-running
	/// stream. Both sides derive the slip's random state from it
	/// (<see cref="ShrapnelGripSlipSeed"/>), which is what makes the operator's screen and the
	/// authority draw the same values without either waiting for the other.
	/// </summary>
	public int GripSlipNonce { get; set; }

	/// <summary>
	/// The values the operator's own screen drew for this slip, read back off the limb that screen
	/// carries. They are EVIDENCE, not a verdict: the authority derives its own from the same seed and
	/// logs a disagreement (<see cref="ShrapnelGripSlipRoller"/>). Meaningless unless
	/// <see cref="GripSlipNonce"/> is non-zero.
	/// </summary>
	public ShrapnelGripSlipValues GripSlipValues { get; set; }

	/// <summary>
	/// True when this report is a semantic ownership/terminal transition (initial
	/// grab, release or break-grasp); it tells the transport that this message is
	/// reliable. Ordinary held-piece position reports remain unreliable.
	/// </summary>
	public bool OwnershipChange { get; set; }
}
