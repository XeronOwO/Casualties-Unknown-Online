using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// What one completed medical operation does to the unit it worked on. A unit is
/// (target, limb, operation kind); the rule is derived from the native semantics
/// (the concurrency audit of the native minigames), not from a blanket lock:
/// <list type="bullet">
/// <item>a UNIQUE unit settles once — relocating one dislocation, removing one
/// splint or tourniquet, amputating one limb. Several operators may start on it
/// (the native engine has no lock), the first completion resolves it, and the
/// other open operations on that unit are answered with
/// <see cref="MedicalOperationTerminalReason.AlreadyHandled"/> and stopped, so
/// nobody keeps working a limb that is already treated.</item>
/// <item>a REPEATABLE effect (bandage, injection, AED, manual defibrillation)
/// applies per completed operation; its operations never settle each other.</item>
/// <item>the shrapnel family's unit is the PIECE, and the shared session already
/// arbitrates it with per-piece ownership: a piece is removed once, and the
/// pieces nobody removed stay in the wound.</item>
/// </list>
/// </summary>
internal static class MedicalOperationUnitRules
{
	/// <summary>True when the first completion settles the unit (a unique unit).</summary>
	internal static bool ResolvesOnce(MedicalOperationKind kind) =>
		kind is MedicalOperationKind.Dislocation
			or MedicalOperationKind.Amputation
			or MedicalOperationKind.SplintRemoval
			or MedicalOperationKind.TourniquetRemoval;

	/// <summary>
	/// The precise answer a losing operator gets. The host logs it, and a client
	/// derives the same sentence from the terminal's reason and kind, so the
	/// "already handled" verdict does not need a reason string on the wire.
	/// </summary>
	internal static string HandledReason(MedicalOperationKind kind) => kind switch
	{
		MedicalOperationKind.Dislocation => "The dislocation has already been reduced by another operator.",
		MedicalOperationKind.Amputation => "The limb has already been amputated by another operator.",
		MedicalOperationKind.SplintRemoval => "The splint has already been removed by another operator.",
		MedicalOperationKind.TourniquetRemoval => "The tourniquet has already been removed by another operator.",
		_ => "The wound has already been treated by another operator.",
	};
}
