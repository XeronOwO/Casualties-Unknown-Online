namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// Runs after the shared unchipped world has served the character, late-layer, pod and last-layer
/// rows. The session's Merc ending must not write either player's solo unlock keys, and the host's
/// native solo picker must come back exactly as it was once the session is left.
/// </summary>
internal sealed class CharacterSoloLockCase : AcceptanceCase
{
	public override string Name => "character-solo-lock";

	public override CaseCost Cost => CaseCost.InRun;

	public override Task RunAsync(CaseContext context, CancellationToken cancellationToken) =>
		CharacterChoiceCase.RunSoloLockAsync(context, cancellationToken);
}
