namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The limb index the medical-operation messages carry, encoded so a real limb survives protobuf's
/// default-zero rule: a non-negative index is stored as <c>index + 1</c> and zero means "no limb"
/// (-1). Protobuf writes no field whose value equals the type's default, so storing limb 0
/// directly made the receiver read its own -1 initializer — measured 2026-11-18: the host's
/// shrapnel dispatch sent limb 0 and the target's body check reported "Shrapnel limb -1".
///
/// <para>
/// The same rule <see cref="PlayerHealRequestMsg"/> and <see cref="PlayerItemUseResultMsg"/>
/// state for their own limb operands; these five messages predate it and now share it.
/// </para>
/// </summary>
internal static class MedicalLimbWire
{
	/// <summary>Stores -1 as zero and a real limb as <c>index + 1</c>.</summary>
	internal static int Encode(int limbIndex) => limbIndex >= 0 ? limbIndex + 1 : 0;

	/// <summary>Reads zero back as -1 and a positive value as <c>value - 1</c>.</summary>
	internal static int Decode(int wire) => wire <= 0 ? -1 : wire - 1;
}
