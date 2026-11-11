using System.Text;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The save-and-continue row, read on the real game: the host asks for a save cut, the cut commits,
/// the host leaves the world and continues from the menu, and the world comes back. The host's own
/// log carries the cut and the continue, so the case judges the lines the product writes.
/// </summary>
internal sealed class SaveContinueCase : AcceptanceCase
{
	public override string Name => "save-continue";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		var save = await context.Session.ActionAsync(context.Session.HostUrl, "console", cancellationToken, ("-Text", "/save")).ConfigureAwait(false);
		context.Read("save", save);

		var committed = await WaitForLogAsync(context, "Cut Command committed", TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);
		Require(committed.Contains("Cut Command committed", StringComparison.Ordinal), "the save cut did not commit");

		await context.Session.ActionAsync(context.Session.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);
		var left = false;
		for (var wait = 0; wait < 20 && !left; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			left = !IsTrue(state, "inWorld");
		}

		Require(left, "the host did not leave the world before continuing");

		var continued = await context.Session.ActionAsync(context.Session.HostUrl, "continue-run", cancellationToken).ConfigureAwait(false);
		context.Read("continue", continued);
		var restored = await WaitForLogAsync(context, "Continue restored world", TimeSpan.FromSeconds(40), cancellationToken).ConfigureAwait(false);
		Require(restored.Contains("Continue restored world", StringComparison.Ordinal), "the continue never restored the world");

		var inWorld = false;
		for (var wait = 0; wait < 30 && !inWorld; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			inWorld = IsTrue(state, "inWorld");
		}

		Require(inWorld, "the host did not enter the restored world");
		context.Read("restored line", JsonDocument.Parse("\"" + Escape(restored) + "\"").RootElement.Clone());

		// The member followed the host out of the world; the continued world needs it back before a
		// later case reads the pair, and the host's refreshed member id replaces the old one.
		var rejoined = await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		context.Note("member after continue: " + rejoined);
		var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		if (members.Length > 0)
		{
			context.UseMember(members[0]);
			context.Note("member id after continue: " + members[0]);
		}
	}

	private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

	private static bool IsTrue(JsonElement answer, string property)
	{
		if (answer.ValueKind == JsonValueKind.Object)
		{
			if (answer.TryGetProperty(property, out var value))
			{
				return value.ValueKind switch
				{
					JsonValueKind.True => true,
					JsonValueKind.String => string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase),
					_ => false,
				};
			}

			foreach (var child in answer.EnumerateObject())
			{
				if (child.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array && IsTrue(child.Value, property))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static async Task<string> WaitForLogAsync(CaseContext context, string needle, TimeSpan budget, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + budget;
		while (DateTime.UtcNow < deadline)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var text = ReadLog(context.HostLogPath);
			if (text.Contains(needle, StringComparison.Ordinal))
			{
				var index = text.LastIndexOf(needle, StringComparison.Ordinal);
				return text[index..Math.Min(text.Length, index + 240)].Split('\n')[0].Trim();
			}
		}

		return string.Empty;
	}

	private static string ReadLog(string path)
	{
		try
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream, Encoding.UTF8);
			return reader.ReadToEnd();
		}
		catch (Exception)
		{
			return string.Empty;
		}
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
