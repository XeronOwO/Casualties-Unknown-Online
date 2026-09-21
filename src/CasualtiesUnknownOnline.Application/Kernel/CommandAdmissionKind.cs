namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// What the admission seam decided about one submitted command.
/// </summary>
public enum CommandAdmissionKind
{
	/// <summary>The submission continues to the kernel, which judges it exactly as before.</summary>
	Admitted,

	/// <summary>The submission is refused and the sender is answered with the reason.</summary>
	Refused,

	/// <summary>
	/// The submission is refused WITHOUT an answer — the shape whose entry point
	/// has always stayed silent (today: a destroy report for an item the sender
	/// neither owns nor sees in the world). The refusal is audited, not announced.
	/// </summary>
	Ignored,
}
