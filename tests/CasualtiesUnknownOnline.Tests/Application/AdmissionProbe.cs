using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Tests.Application;

/// <summary>
/// The admission seam as the two seam suites submit to it, in both spellings a
/// member's report has.
///
/// <para>
/// The seam takes the WIRE KIND as well as the mapped command, because the command
/// cannot carry it: a console write and a native use are deliberately the same
/// kernel fact (<c>WireCommandKind.ItemDataWrite</c> maps to
/// <c>UpdateItemStateCommand</c>), and the host's own rule for the verb is a
/// property of the kind. The suites pin the actor binding, the authority
/// declaration and the item rules — all of which a NATIVE report reaches — so they
/// submit under the kind a native report of the shape they built arrives as, and the
/// console verb's own cases say so by name through
/// <see cref="AdmitConsoleWrite"/>.
/// </para>
/// </summary>
internal static class AdmissionProbe
{
	extension(KernelCommandGateway gateway)
	{
		/// <summary>One member submission of a NATIVE report: the command's own shape decides the kind, exactly as the mapper's arm for it would.</summary>
		internal CommandAdmission Admit(ulong sender, GameCommand command) =>
			gateway.AdmitMemberSubmission(sender, NativeReportKind(command), command);

		/// <summary>
		/// One member submission of a CONSOLE write — the verb the host's own rule owns.
		/// The command is the same <c>UpdateItemStateCommand</c> a native use maps to,
		/// which is the point of the kind.
		/// </summary>
		internal CommandAdmission AdmitConsoleWrite(ulong sender, GameCommand command) =>
			gateway.AdmitMemberSubmission(sender, WireCommandKind.ItemDataWrite, command);
	}

	/// <summary>
	/// The wire kind a member's own client reports a command of this shape under. The
	/// host-authored shapes (a run start, an enemy upsert, a world-entity fact) are
	/// never a member's submission at all: the cases that build them are about the
	/// authority declaration, which the seam judges before any kind-keyed rule, so no
	/// kind can change their verdict — they take the state report's kind, the one a
	/// member-authored item fact uses.
	/// </summary>
	private static WireCommandKind NativeReportKind(GameCommand command) => command switch
	{
		PickUpItemCommand => WireCommandKind.ItemPickup,
		DropItemCommand => WireCommandKind.ItemDrop,
		DestroyItemCommand => WireCommandKind.ItemDestroy,
		UpdateItemStateCommand => WireCommandKind.ItemUpdateState,
		SyncContainerItemsCommand => WireCommandKind.ItemContainerSync,
		ApplyLimbItemCommand => WireCommandKind.ItemApplyLimb,
		RemoveLimbItemCommand => WireCommandKind.ItemRemoveLimb,
		_ => WireCommandKind.ItemUpdateState,
	};
}
