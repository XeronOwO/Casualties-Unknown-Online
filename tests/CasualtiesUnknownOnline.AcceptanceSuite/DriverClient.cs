using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.AcceptanceSuite;

/// <summary>
/// The driver socket: the acceptance channel's eval protocol. One frame in (`eval`, an id and a
/// snippet), one answer out (`eval_result` with the snippet's JSON value, or `eval_error` with the
/// reason); frames belonging to another id are skipped. A snippet that answers nothing is an error
/// here, because a case that silently reads nothing would otherwise pass on a missing answer.
/// </summary>
internal sealed partial class DriverClient(string url, int evalTimeoutMs = 60_000) : IAsyncDisposable
{
	private ClientWebSocket _socket = new();
	private readonly int _evalTimeoutMs = evalTimeoutMs;

	public string Url { get; } = url;

	public async Task ConnectAsync(CancellationToken cancellationToken)
	{
		// The driver is a loopback socket, and this machine's system proxy (a local Clash) answers a
		// CONNECT for 127.0.0.1 with a refusal: the socket then fails before the game is ever asked.
		// A loopback endpoint must never go through a proxy (measured: the suite refused 18591 while
		// a direct ClientWebSocket to the same URL answered).
		_socket.Options.Proxy = null;
		try
		{
			await _socket.ConnectAsync(new Uri(Url), cancellationToken).ConfigureAwait(false);
			await SelectEvaluatorAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is WebSocketException or IOException)
		{
			// A client that is down is a case finding, not a suite crash: every caller records a
			// DriverException as the row's problem (2026-11-20: the host-crash row killed the host driver and
			// the raw WebSocketException escaped the suite's log read at Suite.cs:346).
			throw new DriverException($"the driver at {Url} did not answer: {exception.Message}", exception);
		}
	}

	/// <summary>
	/// The 1.0.2 game no longer ships Mono.CSharp, so HotRepl's default Mono evaluator cannot compile a
	/// snippet. Its Roslyn evaluator is shipped with the plugin; select it per connection before the first
	/// eval. The driver snippets are still written in the one-expression shape both evaluators accept.
	/// </summary>
	private async Task SelectEvaluatorAsync(CancellationToken cancellationToken)
	{
		var id = Guid.NewGuid().ToString("N");
		var frame = JsonSerializer.Serialize(new { type = "select_evaluator", id, evaluator = "Roslyn.Script" });
		await SendAsync(frame, cancellationToken).ConfigureAwait(false);
		while (true)
		{
			var text = await ReceiveAsync(cancellationToken).ConfigureAwait(false);
			using var document = JsonDocument.Parse(text);
			var root = document.RootElement;
			if (!root.TryGetProperty("id", out var idElement)
				|| idElement.ValueKind != JsonValueKind.String
				|| !string.Equals(idElement.GetString(), id, StringComparison.Ordinal))
			{
				continue;
			}

			var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
			if (string.Equals(type, "select_evaluator_result", StringComparison.Ordinal)
				&& root.TryGetProperty("success", out var success)
				&& success.ValueKind == JsonValueKind.True)
			{
				return;
			}

			var detail = root.TryGetProperty("error", out var error) ? error.GetRawText() : text;
			throw new DriverException($"the client refused the Roslyn evaluator: {detail}");
		}
	}

	/// <summary>Reconnects after the game closed the socket (a scene change does this): one retry, then the case fails.</summary>
	public async Task<JsonElement?> EvalAsync(string code, string label, CancellationToken cancellationToken)
		=> await EvalAsync(code, label, cancellationToken, allowNoValue: false).ConfigureAwait(false);

	/// <summary>One eval; <paramref name="allowNoValue"/> is for a snippet that DEFINES a type and answers nothing.</summary>
	public async Task<JsonElement?> EvalAsync(string code, string label, CancellationToken cancellationToken, bool allowNoValue)
	{
		try
		{
			return await EvalOnceAsync(code, label, cancellationToken, allowNoValue).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is WebSocketException or IOException)
		{
			_socket.Dispose();
			_socket = new ClientWebSocket();
			try
			{
				await ConnectAsync(cancellationToken).ConfigureAwait(false);
				return await EvalOnceAsync(code, label, cancellationToken, allowNoValue).ConfigureAwait(false);
			}
			catch (Exception retry) when (retry is WebSocketException or IOException)
			{
				throw new DriverException($"the driver at {Url} dropped while answering '{label}': {retry.Message}", retry);
			}
		}
	}

	private async Task<JsonElement?> EvalOnceAsync(string code, string label, CancellationToken cancellationToken, bool allowNoValue)
	{
		var id = Guid.NewGuid().ToString("N");
		var frame = JsonSerializer.Serialize(new { type = "eval", id, code, timeoutMs = _evalTimeoutMs });
		await SendAsync(frame, cancellationToken).ConfigureAwait(false);

		while (true)
		{
			var text = await ReceiveAsync(cancellationToken).ConfigureAwait(false);
			using var document = JsonDocument.Parse(text);
			var root = document.RootElement;
			if (!root.TryGetProperty("id", out var idElement)
				|| idElement.ValueKind != JsonValueKind.String
				|| !string.Equals(idElement.GetString(), id, StringComparison.Ordinal))
			{
				continue;
			}

			var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
			if (string.Equals(type, "eval_result", StringComparison.Ordinal))
			{
				if (root.TryGetProperty("truncated", out var truncated) && truncated.ValueKind == JsonValueKind.True)
				{
					throw new DriverException($"eval '{label}' answered with a truncated value; the answer is not usable");
				}

				if (!root.TryGetProperty("value", out var value)
					|| value.ValueKind != JsonValueKind.String
					|| string.IsNullOrEmpty(value.GetString()))
				{
					if (allowNoValue)
					{
						return null;
					}

					throw new DriverException($"eval '{label}' returned no value");
				}

				using var inner = JsonDocument.Parse(value.GetString()!);
				return inner.RootElement.Clone();
			}

			if (string.Equals(type, "eval_error", StringComparison.Ordinal))
			{
				var detail = root.TryGetProperty("error", out var error) ? error.GetRawText() : "(no error object)";
				throw new DriverException($"eval '{label}' failed: {detail}");
			}
		}
	}

	/// <summary>
	/// Run one committed recipe from <c>tools/acceptance/recipes</c>: its <c>{{s:key}}</c> placeholders
	/// become string literals and its <c>{{n:key}}</c> ones numbers, both exactly once, as the driver
	/// does — a value the recipe never uses, or a placeholder without a value, fails before the client
	/// sees the snippet.
	/// </summary>
	public async Task<JsonElement?> RecipeAsync(string name, IReadOnlyDictionary<string, string>? args, CancellationToken cancellationToken)
	{
		var path = Path.Combine(RepositoryPaths.Root, "tools", "acceptance", "recipes", name + ".cs");
		var code = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
		var used = new HashSet<string>(StringComparer.Ordinal);
		var expanded = PlaceholderPattern().Replace(code, match =>
		{
			var kind = match.Groups["kind"].Value;
			var key = match.Groups["key"].Value;
			if (!(args?.TryGetValue(key, out var value) ?? false))
			{
				throw new DriverException($"the recipe '{name}' needs an argument for '{key}'");
			}

			if (!used.Add(key))
			{
				throw new DriverException($"the recipe '{name}' places '{key}' more than once");
			}

			if (kind == "n")
			{
				if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
				{
					throw new DriverException($"the recipe '{name}' reads '{key}' as a number, but its value is '{value}'");
				}

				return value;
			}

			return JsonSerializer.Serialize(value);
		});
		return await EvalAsync(expanded, name, cancellationToken).ConfigureAwait(false);
	}

	private async Task SendAsync(string frame, CancellationToken cancellationToken)
	{
		var bytes = Encoding.UTF8.GetBytes(frame);
		await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
	}

	private async Task<string> ReceiveAsync(CancellationToken cancellationToken)
	{
		var buffer = new byte[64 * 1024];
		using var stream = new MemoryStream();
		while (true)
		{
			var result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
			if (result.MessageType == WebSocketMessageType.Close)
			{
				throw new DriverException("the client closed the connection before answering");
			}

			stream.Write(buffer, 0, result.Count);
			if (result.EndOfMessage)
			{
				return Encoding.UTF8.GetString(stream.ToArray());
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_socket.State == WebSocketState.Open)
		{
			try
			{
				await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None).ConfigureAwait(false);
			}
			catch (WebSocketException)
			{
				// A client that already dropped is the case's own finding, not a cleanup failure.
			}
		}

		_socket.Dispose();
	}

	[GeneratedRegex(@"\{\{(?<kind>[sn]):(?<key>[A-Za-z0-9-]+)\}\}")]
	private static partial Regex PlaceholderPattern();
}

/// <summary>
/// A driver call that could not be answered. Not sealed: <see cref="ClientEndedException"/> carries the
/// exit code of a client that had already ended, and every existing handler of an unanswered call must
/// keep handling it.
/// </summary>
internal class DriverException : Exception
{
	public DriverException(string message)
		: base(message)
	{
	}

	public DriverException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}

/// <summary>The repository root: the nearest ancestor of the binary that names the solution.</summary>
internal static class RepositoryPaths
{
	public static string Root { get; } = Find();

	private static string Find()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException("the acceptance suite could not find the repository root above " + AppContext.BaseDirectory);
	}
}
