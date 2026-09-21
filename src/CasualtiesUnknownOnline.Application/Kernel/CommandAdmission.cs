using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The admission seam's verdict: admitted, refused with an answer, or refused
/// silently. The reason is only meaningful when <see cref="Kind"/> is not
/// <see cref="CommandAdmissionKind.Admitted"/>.
/// </summary>
public readonly record struct CommandAdmission(CommandAdmissionKind Kind, RejectionReason? Reason)
{
	public bool IsAdmitted => Kind == CommandAdmissionKind.Admitted;

	/// <summary>True when the caller must answer the sender with <see cref="Reason"/>.</summary>
	public bool AnswersSender => Kind == CommandAdmissionKind.Refused;

	public static CommandAdmission Admit() => new(CommandAdmissionKind.Admitted, null);

	public static CommandAdmission Refuse(RejectionReason reason) => new(CommandAdmissionKind.Refused, reason);

	public static CommandAdmission Ignore(RejectionReason reason) => new(CommandAdmissionKind.Ignored, reason);
}
