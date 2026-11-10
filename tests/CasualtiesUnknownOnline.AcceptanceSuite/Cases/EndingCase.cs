using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The run's ending is the host's, and the run row carries it: the host's own comic or breach
/// cutscene records the kind, the layer advance stamps it into the run baseline, and a member
/// replays the host's kind instead of its own. The case invokes the game's own ending method
/// through the acceptance declaration and reads the recorded kind back.
/// </summary>
internal sealed class EndingCase : AcceptanceCase
{
	public override string Name => "ending";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var declaration = Path.Combine(RepositoryPaths.Root, "tools", "acceptance", "driver", "eval-declarations", "run-ending-a.cs");
		var source = await File.ReadAllTextAsync(declaration, cancellationToken).ConfigureAwait(false);
		var declared = await context.Session.ActionAsync(context.Session.HostUrl, "declare", cancellationToken, ("-Declare", "run-ending-a"), ("-Text", source)).ConfigureAwait(false);
		context.Note("declaration: " + Raw(declared));

		var called = await context.RecipeAsync("run-ending-a", cancellationToken, ("mode", "call"), ("kind", "merc")).ConfigureAwait(false);
		context.Read("call", called);
		var calledText = called is { ValueKind: JsonValueKind.String } callValue ? callValue.GetString() ?? string.Empty : Raw(called);
		Require(calledText.Contains("ending=Merc", StringComparison.Ordinal), $"the ending call answered '{calledText}'");

		var read = await context.RecipeAsync("run-ending-a", cancellationToken, ("mode", "read"), ("kind", "merc")).ConfigureAwait(false);
		context.Read("read", read);
		var recorded = read is { ValueKind: JsonValueKind.String } value ? value.GetString() ?? string.Empty : Raw(read);
		Require(recorded.Contains("ending=Merc", StringComparison.Ordinal), $"the run recorded '{recorded}', not the host's Merc ending");
	}

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

	private static string StringValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty(property, out var value)
			&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";
}
