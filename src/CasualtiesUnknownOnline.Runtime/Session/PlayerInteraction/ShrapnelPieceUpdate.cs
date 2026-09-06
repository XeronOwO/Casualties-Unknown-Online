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
	/// True when this report is a semantic ownership/terminal transition (initial
	/// grab, release or break-grasp); it tells the transport that this message is
	/// reliable. Ordinary held-piece position reports remain unreliable.
	/// </summary>
	public bool OwnershipChange { get; set; }
}
