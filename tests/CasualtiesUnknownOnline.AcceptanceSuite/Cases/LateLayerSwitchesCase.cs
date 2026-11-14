using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The run's live switches at world start are the host's, on both clients. A member used to call
/// <c>PreRunScript.StartRun</c> as soon as the menu existed, before the host's run baseline arrived; the
/// native <c>WorldGeneration.Start</c> then derived <c>unchipped</c> and the rest of the switch family from
/// the member's own menu table, and the later baseline write did not re-derive them. The last-layer
/// cutscene consults exactly that switch (<c>EndLabScript.cs:88-91</c>), so a member of an unchipped run
/// could not attack or use after the cutscene. This case starts the game's own <c>unchipped</c> preset on
/// the host, joins the member, and reads the derived switch on each client.
/// </summary>
internal sealed class LateLayerSwitchesCase : AcceptanceCase
{
	public override string Name => "late-layer-switches";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var preset = await context.RecipeAsync("run-preset", cancellationToken, ("name", "unchipped")).ConfigureAwait(false);
		context.Read("preset", preset);
		Require(Boolean(preset, "ok") == true, "the host's run preset could not be selected: " + Raw(preset));

		var restart = await context.Session.RestartRunAsync(cancellationToken).ConfigureAwait(false);
		context.Note(restart);
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

			var host = await context.RecipeAsync("run-switch-state", cancellationToken).ConfigureAwait(false);
			context.Read("host", host);
			var member = await context.MemberRecipeAsync("run-switch-state", cancellationToken).ConfigureAwait(false);
			context.Read("member", member);

			Require(Boolean(host, "ok") == true, "the host's switches could not be read: " + Raw(host));
			Require(Boolean(member, "ok") == true, "the member's switches could not be read: " + Raw(member));
			Require(Boolean(host, "setting") == true, "the host's run settings do not carry unchipped=true: " + Raw(host));
			Require(Boolean(host, "unchipped") == true, "the host's WorldGeneration.unchipped is not true: " + Raw(host));
			Require(Boolean(member, "setting") == true, "the member's run settings do not carry unchipped=true: " + Raw(member));
			Require(Boolean(member, "unchipped") == true, "the member's WorldGeneration.unchipped did not follow the host's baseline: " + Raw(member));
		}
		finally
		{
			// Put the shared session back on the normal preset: the unchipped table lives on the same
			// PreRunScript instance the rest of the suite reuses, and a later fresh run would otherwise
			// start from it. The restart is the game's own entry, not a menu write.
			try
			{
				await context.RecipeAsync("run-preset", cancellationToken, ("name", "normal")).ConfigureAwait(false);
				await context.Session.RestartRunAsync(cancellationToken).ConfigureAwait(false);
				await context.RecipeAsync("run-preset", cancellationToken, ("name", "")).ConfigureAwait(false);
			}
			catch (Exception exception)
			{
				context.Note("could not restore the normal preset after the case: " + exception.Message);
			}
		}
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.True
			: null;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
