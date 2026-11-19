using System.Globalization;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite;

/// <summary>
/// The <c>drive</c> subcommand: one driver action against a running client, for manual probes (the port
/// of <c>tools/acceptance/drive-in-process.ps1</c>). The suite's own action client does the work, so the
/// vocabulary and the answers are identical to what a case sees.
/// </summary>
internal static class DriveCommand
{
	private const string Usage =
		"drive --url <ws> --action <action> [--control <id>] [--text <text>] [--lobby <id>] [--page <page>] "
		+ "[--recipe <name>] [--arg k=v]... [--declare <name>] [--json <path>] [--timeout-ms <n>]";

	private static readonly string[] Actions =
	[
		"ping", "state", "open-window", "goto-page", "click", "set-text", "create-lobby", "join-lobby", "start-run",
		"continue-run", "leave-world", "console", "console-ui", "console-accept", "console-arrow", "quit", "wait-log",
		"wait-state", "log-tail", "recipe", "declare", "select-character",
	];

	public static async Task<int> RunAsync(string[] args)
	{
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var recipeArguments = new List<string>();
		for (var index = 0; index < args.Length; index++)
		{
			var token = args[index];
			if (!token.StartsWith("--", StringComparison.Ordinal))
			{
				continue;
			}

			var name = token[2..];
			var value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
				? args[++index]
				: "true";
			if (string.Equals(name, "arg", StringComparison.OrdinalIgnoreCase))
			{
				recipeArguments.Add(value);
			}
			else
			{
				values[name] = value;
			}
		}

		if (values.TryGetValue("help", out _) || values.ContainsKey("list-actions"))
		{
			Console.WriteLine(Usage);
			Console.WriteLine("actions: " + string.Join(", ", Actions));
			return 0;
		}

		if (!values.TryGetValue("url", out var url) || string.IsNullOrWhiteSpace(url))
		{
			Console.Error.WriteLine("missing --url; usage: " + Usage);
			return 64;
		}

		if (!values.TryGetValue("action", out var action) || string.IsNullOrWhiteSpace(action))
		{
			Console.Error.WriteLine("missing --action; usage: " + Usage);
			return 64;
		}

		action = action.ToLowerInvariant();
		var timeoutMs = values.TryGetValue("timeout-ms", out var timeoutText)
			&& int.TryParse(timeoutText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTimeout)
			? parsedTimeout
			: 30_000;
		var control = values.GetValueOrDefault("control");
		var text = values.GetValueOrDefault("text");
		var lobby = values.GetValueOrDefault("lobby");
		var page = values.GetValueOrDefault("page");
		var declaration = values.GetValueOrDefault("declare");
		var recipe = values.GetValueOrDefault("recipe");

		// The eval inside the action budget: half the budget, clamped to what a generated snippet can
		// take. A fixed eval timeout reports a loaded machine's slow answer as a missing value.
		var evalTimeoutMs = Math.Max(10_000, Math.Min(90_000, timeoutMs / 2));
		var options = new Options { HostUrl = url, DriverEvalTimeoutMs = evalTimeoutMs };
		var session = new SessionDriver(options);
		using var cancellation = new CancellationTokenSource(timeoutMs);

		try
		{
			JsonElement? answer;
			if (action == "recipe")
			{
				if (string.IsNullOrWhiteSpace(recipe))
				{
					Console.Error.WriteLine("--recipe is required for --action recipe; usage: " + Usage);
					return 64;
				}

				answer = await session.RecipeAtAsync(url, recipe, ParsePairs(recipeArguments), cancellation.Token).ConfigureAwait(false);
			}
			else if (action == "declare")
			{
				if (string.IsNullOrWhiteSpace(declaration))
				{
					Console.Error.WriteLine("--declare is required for --action declare; usage: " + Usage);
					return 64;
				}

				answer = await session.ActionAsync(url, "declare", cancellation.Token, ("-Declare", declaration)).ConfigureAwait(false);
			}
			else
			{
				var extra = new List<(string, string)>();
				if (control is not null)
				{
					extra.Add(("-Control", control));
				}

				if (text is not null)
				{
					extra.Add(("-Text", text));
				}

				if (lobby is not null)
				{
					extra.Add(("-LobbyId", lobby));
				}

				if (page is not null)
				{
					extra.Add(("-Page", page));
				}

				answer = await session.ActionAsync(url, action, cancellation.Token, [.. extra]).ConfigureAwait(false);
			}

			if (answer is null)
			{
				Console.Error.WriteLine($"the client answered '{action}' with no value");
				return 1;
			}

			var json = answer.Value.GetRawText();
			Console.WriteLine(json);
			if (answer.Value.ValueKind == JsonValueKind.Object
				&& answer.Value.TryGetProperty("ok", out var ok)
				&& ok.ValueKind == JsonValueKind.False)
			{
				// The driver answered, and the answer is a refusal (a control never offered, a lobby
				// that never appeared): the same failure the cases raise, in the console's words.
				Console.Error.WriteLine($"the client refused '{action}': " + json);
				return 1;
			}
			if (values.TryGetValue("json", out var jsonPath) && !string.IsNullOrWhiteSpace(jsonPath))
			{
				var directory = Path.GetDirectoryName(Path.GetFullPath(jsonPath));
				if (!string.IsNullOrEmpty(directory))
				{
					Directory.CreateDirectory(directory);
				}

				await File.WriteAllTextAsync(jsonPath, json, cancellation.Token).ConfigureAwait(false);
			}

			return 0;
		}
		catch (Exception exception) when (IsTransport(exception))
		{
			Console.Error.WriteLine("transport failure (" + exception.GetType().Name + "): " + exception.Message);
			return 2;
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
			Console.Error.WriteLine($"the action '{action}' exceeded its {timeoutMs} ms budget");
			return 3;
		}
		catch (OperationCanceledException exception)
		{
			// The socket gave up before our own budget did: the client is not there.
			Console.Error.WriteLine("transport failure (" + exception.GetType().Name + "): " + exception.Message);
			return 2;
		}
		catch (DriverException exception) when (IsUsageError(exception))
		{
			Console.Error.WriteLine(exception.Message);
			return 64;
		}
		catch (DriverException exception) when (IsEvalTimeout(exception))
		{
			Console.WriteLine("{\"error\":\"timeout\",\"detail\":" + JsonSerializer.Serialize(exception.Message) + "}");
			return 3;
		}
		catch (DriverException exception)
		{
			Console.Error.WriteLine(exception.Message);
			return 1;
		}
		catch (WebSocketException exception)
		{
			Console.Error.WriteLine("transport failure: " + exception.Message);
			return 2;
		}
		catch (HttpRequestException exception)
		{
			Console.Error.WriteLine("transport failure: " + exception.Message);
			return 2;
		}
		catch (IOException exception)
		{
			Console.Error.WriteLine("transport failure: " + exception.Message);
			return 2;
		}
	}

	private static bool IsTransport(Exception exception)
	{
		for (var current = exception; current is not null; current = current.InnerException)
		{
			if (current is WebSocketException
				or HttpRequestException
				or SocketException)
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsUsageError(DriverException exception) =>
		exception.Message.Contains("needs an argument", StringComparison.Ordinal)
		|| exception.Message.Contains("places '", StringComparison.Ordinal)
		|| exception.Message.Contains("reads '", StringComparison.Ordinal);

	private static bool IsEvalTimeout(DriverException exception) =>
		exception.Message.Contains("evalTimeout", StringComparison.Ordinal)
		|| exception.Message.Contains("\"Timeout\"", StringComparison.Ordinal)
		|| exception.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);

	private static Dictionary<string, string> ParsePairs(IEnumerable<string> pairs)
	{
		var values = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var pair in pairs)
		{
			var separator = pair.IndexOf('=', StringComparison.Ordinal);
			if (separator <= 0)
			{
				throw new InvalidOperationException($"recipe argument '{pair}' is not key=value");
			}

			values[pair[..separator]] = pair[(separator + 1)..];
		}

		return values;
	}
}
