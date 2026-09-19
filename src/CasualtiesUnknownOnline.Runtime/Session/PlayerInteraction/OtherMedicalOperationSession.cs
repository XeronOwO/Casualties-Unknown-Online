using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One host-owned Stage 3 medical operation session: one operator, one target limb
/// and one kind. Several such sessions may be open at the same time — different
/// kinds on one limb, or one kind worked by several operators at once — because the
/// shared claim book arbitrates only the item instance and the operator slot. A unit
/// whose outcome resolves once is settled by its FIRST completion and the other
/// sessions open on that unit are stopped there
/// (<see cref="MedicalOperationUnitRules"/>).
/// </summary>
internal sealed class OtherMedicalOperationSession
{
	internal ulong OperationId;
	internal ulong Operator;
	internal ulong Target;
	internal ulong ItemInstanceId;
	internal string ItemId = "";
	internal int LimbIndex = -1;
	internal MedicalOperationKind Kind;
	internal long StartedMs;
	internal long LastUpdateMs;
	internal int Sequence;

	/// <summary>Bandage consumed fraction or amputation cut progress (0..1).</summary>
	internal float Progress;

	/// <summary>Manual defib elapsed-drain already committed (seconds).</summary>
	internal float ManualDefibSeconds;

	internal bool AedStartDrained;
	internal bool AedAnalyzeDrained;
	internal bool AedShockApplied;

	/// <summary>Instance id of the item awarded by a removal, if any.</summary>
	internal ulong AwardedItemId;
}
