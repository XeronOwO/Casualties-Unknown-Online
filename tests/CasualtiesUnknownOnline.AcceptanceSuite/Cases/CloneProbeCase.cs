using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The host's view of a teammate is CUO's own clone, not the game's body: the clone must be on the
/// host's client with its marker and its components, and the facing probe must answer. The row is the
/// sweep's "the third party sees the teammate" reading — a clone that is missing or carries no marker
/// means the display surface broke.
/// </summary>
internal sealed class CloneProbeCase : AcceptanceCase
{
	public override string Name => "clone-probe";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		_ = await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		var probe = await context.RecipeAsync("remote-clone-probe", cancellationToken, ("mode", "components"));
		context.Read("remote-clone-probe", probe);
		Require(Boolean(probe, "ok"), "the clone probe did not answer ok: " + Raw(probe));

		var clones = Integer(probe, "cloneCount");
		if (clones < 1)
		{
			throw new AssertionException($"the host's view carries no remote clone (cloneCount {clones})");
		}

		var components = StringValue(probe, "components");
		if (!components.Contains("RemoteCloneRender", StringComparison.Ordinal))
		{
			throw new AssertionException("the remote clone does not carry CUO's marker: " + Trim(components));
		}

		var facing = await context.RecipeAsync("remote-clone-probe", cancellationToken, ("mode", "facing"));
		context.Read("remote-clone-probe-facing", facing);
		Require(Boolean(facing, "ok"), "the clone's facing probe did not answer ok: " + Raw(facing));

		context.Note($"clones {clones}; the clone carries RemoteCloneRender and answers the facing probe");
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string Trim(string text) => text.Length <= 200 ? text : text[..200] + "…";

	private static string StringValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	private static int Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
		&& value.TryGetInt32(out var number)
			? number
			: 0;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
