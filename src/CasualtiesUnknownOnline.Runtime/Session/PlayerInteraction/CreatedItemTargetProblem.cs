namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Which created-item guard a target fails. The RULES live in <c>PlayerInventoryTakeService.CheckCreatedItemTarget</c>;
/// the SENTENCES live in the give verb's catalogue (`give.answer.refused_*`), because the console contract says every
/// refusal names its own reason in the reader's language.
/// </summary>
internal enum CreatedItemTargetProblem
{
	NotHost,
	Malformed,
	NotInWorld,
	NoSnapshot,
}
