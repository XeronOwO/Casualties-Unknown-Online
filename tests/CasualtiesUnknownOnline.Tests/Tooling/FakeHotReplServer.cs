using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// A throwaway HotRepl endpoint for the driver's black-box tests: it speaks just enough of the
/// websocket protocol the evaluator does — one <c>eval</c> frame in, one <c>eval_result</c> or
/// <c>eval_error</c> frame out — and records every eval snippet it received, so a test asserts what
/// the script actually sent instead of trusting its own retelling.
/// </summary>
internal sealed class FakeHotReplServer : IDisposable
{
	private readonly HttpListener _listener;
	private readonly List<string> _frames = [];
	private readonly object _gate = new();
	private readonly CancellationTokenSource _stopping = new();
	private readonly Task _pump;
	private readonly int _port;

	internal FakeHotReplServer()
	{
		_listener = new HttpListener();
		var attempt = 0;
		while (true)
		{
			_port = FreePort();
			try
			{
				_listener.Prefixes.Clear();
				_listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
				_listener.Start();
				break;
			}
			catch (HttpListenerException) when (++attempt < 4)
			{
				// The port was taken between the probe and the bind; try another one.
			}
		}

		Url = $"ws://127.0.0.1:{_port}/";
		_pump = Task.Run(PumpAsync);
	}

	/// <summary>The endpoint a driver run points at.</summary>
	internal string Url { get; }

	/// <summary>One received frame in, the reply JSON out; null means "never answer" (the timeout case).</summary>
	internal Func<string, string?> Handler { get; set; } = _ => null;

	/// <summary>The raw frames received so far, in order.</summary>
	internal IReadOnlyList<string> Frames
	{
		get
		{
			lock (_gate)
			{
				return _frames.ToArray();
			}
		}
	}

	/// <summary>True when a frame asks for the given driver command (matching the substituted literal, not a template comment).</summary>
	internal static bool AsksFor(string frame, string command) =>
		frame.Contains("var command = \\\"" + command + "\\\"", StringComparison.Ordinal);

	/// <summary>True when a frame carries the given driver argument (a control id, page or lobby id).</summary>
	internal static bool Argues(string frame, string argument) =>
		frame.Contains("var argument = \\\"" + argument + "\\\"", StringComparison.Ordinal);

	/// <summary>True when a frame carries the given driver text value.</summary>
	internal static bool Texted(string frame, string text) =>
		frame.Contains("var text = \\\"" + text + "\\\"", StringComparison.Ordinal);

	/// <summary>Wraps a client JSON answer into the <c>eval_result</c> frame the evaluator would send.</summary>
	internal static string ReplyFor(string requestFrame, string clientJson) =>
		"{\"type\":\"eval_result\",\"id\":\"" + RequestId(requestFrame) + "\",\"value\":\"" + JsonEscape(clientJson)
			+ "\",\"valueType\":\"System.String\",\"truncated\":false,\"durationMs\":1}";

	/// <summary>Wraps a compile/runtime failure into the <c>eval_error</c> frame the evaluator would send.</summary>
	internal static string EvalErrorFor(string requestFrame, string message) =>
		"{\"type\":\"eval_error\",\"id\":\"" + RequestId(requestFrame) + "\",\"error\":{\"kind\":\"Compile\",\"code\":\"compileError\",\"message\":\"" + JsonEscape(message) + "\"}}";

	/// <summary>The frame the evaluator sends when it hard-aborts a snippet that outran its own timeout (HotRepl's <c>ErrorKind.Timeout</c>).</summary>
	internal static string EvalTimeoutFor(string requestFrame) =>
		"{\"type\":\"eval_error\",\"id\":\"" + RequestId(requestFrame) + "\",\"error\":{\"kind\":\"Timeout\",\"code\":\"evalTimeout\",\"message\":\"the evaluation timed out\"}}";

	/// <summary>An <c>eval_result</c> the client flagged as truncated (the payload is not usable).</summary>
	internal static string ReplyTruncated(string requestFrame) =>
		"{\"type\":\"eval_result\",\"id\":\"" + RequestId(requestFrame) + "\",\"value\":null,\"valueType\":\"System.String\",\"truncated\":true,\"durationMs\":1}";

	/// <summary>An <c>eval_result</c> for an input that had nothing to return — the shape a type
	/// declaration has (<c>value: null</c> and NOT truncated, which is what the driver's own
	/// no-value path distinguishes).</summary>
	internal static string ReplyWithoutValue(string requestFrame) =>
		"{\"type\":\"eval_result\",\"id\":\"" + RequestId(requestFrame) + "\",\"value\":null,\"valueType\":null,\"truncated\":false,\"durationMs\":1}";

	internal static int FreePort()
	{
		var probe = new TcpListener(IPAddress.Loopback, 0);
		probe.Start();
		var port = ((IPEndPoint)probe.LocalEndpoint).Port;
		probe.Stop();
		return port;
	}

	private static string RequestId(string frame)
	{
		var match = Regex.Match(frame, "\"id\"\\s*:\\s*\"([^\"]*)\"");
		return match.Success ? match.Groups[1].Value : string.Empty;
	}

	private static string JsonEscape(string value) =>
		value.Replace("\\", "\\\\")
			.Replace("\"", "\\\"")
			.Replace("\r", "\\r")
			.Replace("\n", "\\n");

	private async Task PumpAsync()
	{
		while (!_stopping.IsCancellationRequested)
		{
			HttpListenerContext context;
			try
			{
				context = await _listener.GetContextAsync().ConfigureAwait(false);
			}
			catch (Exception)
			{
				return; // the listener was stopped
			}

			if (!context.Request.IsWebSocketRequest)
			{
				context.Response.StatusCode = 400;
				context.Response.Close();
				continue;
			}

			var accepted = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
			await HandleAsync(accepted.WebSocket).ConfigureAwait(false);
		}
	}

	private async Task HandleAsync(WebSocket socket)
	{
		var buffer = new byte[64 * 1024];
		var text = new StringBuilder();
		try
		{
			while (socket.State == WebSocketState.Open)
			{
				var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ConfigureAwait(false);
				if (received.MessageType == WebSocketMessageType.Close)
				{
					break;
				}

				text.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
				if (!received.EndOfMessage)
				{
					continue;
				}

				var frame = text.ToString();
				text.Clear();
				lock (_gate)
				{
					_frames.Add(frame);
				}

				var reply = Handler(frame);
				if (reply is null)
				{
					continue;
				}

				var bytes = Encoding.UTF8.GetBytes(reply);
				await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
			}
		}
		catch (WebSocketException)
		{
			// the client went away first; the recorded frames are what the test asserts on
		}
		finally
		{
			socket.Dispose();
		}
	}

	public void Dispose()
	{
		_stopping.Cancel();
		try
		{
			_listener.Stop();
			_listener.Close();
		}
		catch (Exception)
		{
			// already stopped
		}

		_stopping.Dispose();
		_ = _pump;
	}
}
