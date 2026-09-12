namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// One field of a restore's native character write plan
/// (<see cref="CharacterNativeFieldPolicy.Plan"/>): whether it can be written at
/// all, what it is called in the account, and — when it cannot — why.
/// </summary>
/// <param name="Kind">Which of the three fields this entry is about.</param>
/// <param name="Applied">True = the adapter writes it; false = the value cannot be written and the refusal is reported instead.</param>
/// <param name="Description">The field with the value it holds, for the account the restore logs.</param>
/// <param name="Refusal">Why the value cannot be written; null when it can.</param>
internal readonly record struct NativeFieldWrite(
	NativeFieldKind Kind,
	bool Applied,
	string Description,
	string? Refusal)
{
	internal static NativeFieldWrite Writable(NativeFieldKind kind, string description) =>
		new(kind, true, description, null);

	internal static NativeFieldWrite Refused(NativeFieldKind kind, string description, string refusal) =>
		new(kind, false, description, refusal);
}
