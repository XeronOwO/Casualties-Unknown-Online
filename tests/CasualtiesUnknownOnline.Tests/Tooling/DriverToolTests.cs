using System;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// The black-box contract of tools/acceptance/drive-in-process.ps1: the script is the acceptance run's
/// hand inside a running client, so what it sends, in what order, and how it fails when the client or
/// the setup is not there are pinned against a fake evaluator endpoint — never against the real one,
/// which only a session batch can provide. The in-process template's own rule (C# 7 parseable, no
/// OS-level input) is gated in the normative suite.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ToolProcessCollection.Name)]
public class DriverToolTests
{
	[Fact]
	public void ListActions_NamesTheWholeVocabularyWithoutAClient()
	{
		var run = DriverToolHarness.Run("-ListActions");

		Assert.Equal(0, run.ExitCode);
		foreach (var action in new[] { "ping", "state", "open-window", "goto-page", "click", "set-text", "create-lobby", "join-lobby", "start-run", "quit", "recipe" })
		{
			Assert.True(run.Output.Contains(action, StringComparison.Ordinal), $"the vocabulary does not name '{action}':{Environment.NewLine}{run.Output}");
		}
	}

	[Fact]
	public void Ping_ReportsTheClientsFacts()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"command\":\"ping\",\"plugin\":true,\"overlay\":true,\"steamInit\":true,\"controlCount\":3}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "ping");

		Assert.Equal(0, run.ExitCode);
		Assert.Equal("true", DriverToolHarness.Field(run.Output, "overlay"));
		var frames = server.Frames;
		Assert.Single(frames);
		Assert.True(FakeHotReplServer.AsksFor(frames[0], "ping"), "ping is its own verb");
	}

	[Fact]
	public void Ping_WithoutTheCuoUiFails()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":false,\"error\":\"no-cuo-online-ui\",\"detail\":\"the evaluator answered but CUO is not here\"}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "ping");

		Assert.Equal(1, run.ExitCode);
		Assert.True(run.Output.Contains("no-cuo-online-ui", StringComparison.Ordinal), "a client without CUO is not a drivable client");
	}

	[Fact]
	public void State_SendsOneEvalFrameAndReturnsTheClientsAnswer()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"lobby\":\"4242\",\"role\":\"Host\",\"controls\":[\"home.create_lobby\",\"tab.home\"]}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "state", "-TimeoutMs", "20000");

		Assert.Equal(0, run.ExitCode);
		Assert.Equal("4242", DriverToolHarness.Field(run.Output, "lobby"));
		Assert.Equal("Host", DriverToolHarness.Field(run.Output, "role"));
		Assert.True(run.Output.Contains("home.create_lobby", StringComparison.Ordinal), "the offered control ids must survive into the driver's answer");

		var frames = server.Frames;
		Assert.Single(frames);
		Assert.True(frames[0].Contains("\"type\":\"eval\"", StringComparison.Ordinal), "the frame must speak the evaluator's protocol");
		Assert.True(FakeHotReplServer.AsksFor(frames[0], "state"), "the frame must ask for the state command");
		Assert.True(frames[0].Contains("CasualtiesUnknownOnline.Runtime.CuoBootstrap", StringComparison.Ordinal), "the template must be the committed in-process half, not a hand-rolled snippet");
		Assert.False(frames[0].Contains("{{", StringComparison.Ordinal), "every placeholder must be substituted before the frame is sent");
		Assert.True(frames[0].Contains("\"timeoutMs\":10000", StringComparison.Ordinal), "the eval timeout is derived from the action budget (min 10 s here)");
	}

	[Fact]
	public void CreateLobby_OpensTheWindowClicksTheCreateControlAndWaitsForTheLobby()
	{
		using var server = new FakeHotReplServer();
		var states = 0;
		server.Handler = frame =>
		{
			if (FakeHotReplServer.AsksFor(frame, "state"))
			{
				states++;
				return FakeHotReplServer.ReplyFor(frame, states == 1
					? "{\"ok\":true,\"lobby\":\"0\",\"windowVisible\":false,\"page\":\"Home\"}"
					: "{\"ok\":true,\"lobby\":\"4242\",\"role\":\"Host\",\"windowVisible\":true,\"page\":\"Home\"}");
			}

			if (FakeHotReplServer.AsksFor(frame, "open-window"))
			{
				return FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"visible\":true,\"page\":\"Home\"}");
			}

			if (FakeHotReplServer.AsksFor(frame, "click") && FakeHotReplServer.Argues(frame, "home.create_lobby"))
			{
				return FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"offered\":true,\"applied\":true,\"controls\":[\"home.create_lobby\"]}");
			}

			return FakeHotReplServer.ReplyFor(frame, "{\"ok\":false,\"error\":\"unexpected\",\"detail\":\"unexpected frame\"}");
		};

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "create-lobby");

		Assert.Equal(0, run.ExitCode);
		Assert.Equal("4242", DriverToolHarness.Field(run.Output, "lobby"));
		Assert.Equal("Host", DriverToolHarness.Field(run.Output, "role"));

		var frames = server.Frames;
		Assert.Equal(4, frames.Count);
		Assert.True(FakeHotReplServer.AsksFor(frames[0], "state"), "the pre-check reads the lobby before opening anything");
		Assert.True(FakeHotReplServer.AsksFor(frames[1], "open-window"), "the window must be open before the Home control exists");
		Assert.True(FakeHotReplServer.AsksFor(frames[2], "click") && FakeHotReplServer.Argues(frames[2], "home.create_lobby"), "the create control is the Online UI's own id");
		Assert.True(FakeHotReplServer.AsksFor(frames[3], "state"), "the lobby must be waited for, not assumed");
	}

	[Fact]
	public void CreateLobby_OnAGuestInSomeoneElsesLobbyFails()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"lobby\":\"4242\",\"role\":\"Guest\"}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "create-lobby");

		Assert.Equal(1, run.ExitCode);
		Assert.True(run.Output.Contains("already-in-lobby", StringComparison.Ordinal), "a guest's lobby is not a lobby this client created");
		Assert.Single(server.Frames);
	}

	[Fact]
	public void JoinLobby_TypesTheIdThenClicksJoinAndWaitsForTheLobby()
	{
		const string lobbyId = "109775243052064263";
		using var server = new FakeHotReplServer();
		var states = 0;
		server.Handler = frame =>
		{
			if (FakeHotReplServer.AsksFor(frame, "state"))
			{
				states++;
				return FakeHotReplServer.ReplyFor(frame, states switch
				{
					1 => "{\"ok\":true,\"lobby\":\"0\",\"page\":\"Home\",\"windowVisible\":false,\"lobbyIdInput\":\"\"}",
					2 => "{\"ok\":true,\"lobby\":\"0\",\"page\":\"Home\",\"windowVisible\":true,\"lobbyIdInput\":\"\"}",
					3 => "{\"ok\":true,\"lobby\":\"0\",\"page\":\"Home\",\"windowVisible\":true,\"lobbyIdInput\":\"" + lobbyId + "\"}",
					_ => "{\"ok\":true,\"lobby\":\"" + lobbyId + "\",\"page\":\"Home\",\"windowVisible\":true,\"role\":\"Guest\"}"
				});
			}

			if (FakeHotReplServer.AsksFor(frame, "open-window"))
			{
				return FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"visible\":true,\"page\":\"Home\"}");
			}

			if (FakeHotReplServer.AsksFor(frame, "set-text"))
			{
				return FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"offered\":true,\"applied\":true,\"controlId\":\"home.lobby_id\"}");
			}

			if (FakeHotReplServer.AsksFor(frame, "click") && FakeHotReplServer.Argues(frame, "home.join"))
			{
				return FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"offered\":true,\"applied\":true,\"controlId\":\"home.join\"}");
			}

			return FakeHotReplServer.ReplyFor(frame, "{\"ok\":false,\"error\":\"unexpected\",\"detail\":\"unexpected frame\"}");
		};

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "join-lobby", "-LobbyId", lobbyId);

		Assert.Equal(0, run.ExitCode);
		Assert.Equal(lobbyId, DriverToolHarness.Field(run.Output, "lobby"));

		var frames = server.Frames;
		Assert.Equal(7, frames.Count);
		Assert.True(FakeHotReplServer.AsksFor(frames[3], "set-text") && FakeHotReplServer.Argues(frames[3], "home.lobby_id") && FakeHotReplServer.Texted(frames[3], lobbyId), "the id is typed into the Online UI's own field");
		Assert.True(FakeHotReplServer.AsksFor(frames[5], "click") && FakeHotReplServer.Argues(frames[5], "home.join"), "the join is the Online UI's own control");
	}

	[Fact]
	public void GotoPage_ClicksTheTabAndConfirmsThePage()
	{
		using var server = new FakeHotReplServer();
		var states = 0;
		server.Handler = frame =>
		{
			if (FakeHotReplServer.AsksFor(frame, "state"))
			{
				states++;
				return FakeHotReplServer.ReplyFor(frame, states == 1
					? "{\"ok\":true,\"windowVisible\":true,\"page\":\"Home\"}"
					: "{\"ok\":true,\"windowVisible\":true,\"page\":\"Players\"}");
			}

			if (FakeHotReplServer.AsksFor(frame, "click"))
			{
				return FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"offered\":true,\"applied\":true,\"controls\":[\"tab.players\"]}");
			}

			return FakeHotReplServer.ReplyFor(frame, "{\"ok\":false,\"error\":\"unexpected\",\"detail\":\"unexpected frame\"}");
		};

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "goto-page", "-Page", "players");

		Assert.Equal(0, run.ExitCode);
		Assert.Equal("Players", DriverToolHarness.Field(run.Output, "page"));

		var frames = server.Frames;
		Assert.Equal(3, frames.Count);
		Assert.True(FakeHotReplServer.AsksFor(frames[1], "click") && FakeHotReplServer.Argues(frames[1], "tab.players"), "the page is switched by its own tab, never by writing the state");
	}

	[Fact]
	public void StartRun_CallsTheNativeEntryAndWaitsForTheWorld()
	{
		using var server = new FakeHotReplServer();
		var states = 0;
		server.Handler = frame =>
		{
			if (FakeHotReplServer.AsksFor(frame, "state"))
			{
				states++;
				return FakeHotReplServer.ReplyFor(frame, states == 1
					? "{\"ok\":true,\"inWorld\":false,\"gateWaiting\":false,\"role\":\"Host\",\"lobby\":\"4242\"}"
					: "{\"ok\":true,\"inWorld\":false,\"gateWaiting\":true,\"role\":\"Host\",\"lobby\":\"4242\"}");
			}

			if (FakeHotReplServer.AsksFor(frame, "start-run"))
			{
				return FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"called\":true,\"inWorld\":false,\"gateWaiting\":false}");
			}

			return FakeHotReplServer.ReplyFor(frame, "{\"ok\":false,\"error\":\"unexpected\",\"detail\":\"unexpected frame\"}");
		};

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "start-run");

		Assert.Equal(0, run.ExitCode);
		Assert.Equal("true", DriverToolHarness.Field(run.Output, "gateWaiting"));

		var frames = server.Frames;
		Assert.Equal(3, frames.Count);
		Assert.True(FakeHotReplServer.AsksFor(frames[1], "start-run"), "the native start entry is its own verb, never a hand-written Steam call");
	}

	[Fact]
	public void Click_RetriesUntilTheFrameOffersTheControl()
	{
		using var server = new FakeHotReplServer();
		var attempts = 0;
		server.Handler = frame =>
		{
			if (FakeHotReplServer.AsksFor(frame, "click"))
			{
				attempts++;
				return FakeHotReplServer.ReplyFor(frame, attempts < 3
					? "{\"ok\":true,\"offered\":false,\"applied\":false,\"controls\":[\"tab.home\"]}"
					: "{\"ok\":true,\"offered\":true,\"applied\":true,\"controls\":[\"home.create_lobby\"]}");
			}

			return FakeHotReplServer.ReplyFor(frame, "{\"ok\":false,\"error\":\"unexpected\",\"detail\":\"unexpected frame\"}");
		};

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "click", "-ControlId", "home.create_lobby", "-RetryDelayMs", "50");

		Assert.Equal(0, run.ExitCode);
		Assert.Equal(3, attempts);
		Assert.Equal(3, server.Frames.Count);
		Assert.True(FakeHotReplServer.Argues(server.Frames[2], "home.create_lobby"), "every retry must ask for the same control id");
	}

	[Fact]
	public void Click_OnAControlThatIsNeverOfferedFailsWithTheLastFrame()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"offered\":false,\"applied\":false,\"controls\":[\"tab.home\"]}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "click", "-ControlId", "home.create_lobby", "-TimeoutMs", "500", "-RetryDelayMs", "50");

		Assert.Equal(1, run.ExitCode);
		Assert.True(run.Output.Contains("control-not-offered", StringComparison.Ordinal), run.Output);
		Assert.True(DriverToolHarness.HasField(run.Output, "last"), "the failure must carry the frame that was last offered");
	}

	[Fact]
	public void EvalError_IsReportedAsADriverFailure()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.EvalErrorFor(frame, "boom");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "state");

		Assert.Equal(1, run.ExitCode);
		Assert.True(run.Output.Contains("eval-error", StringComparison.Ordinal), run.Output);
		Assert.True(run.Output.Contains("boom", StringComparison.Ordinal), "the client's message must reach the caller");
	}

	[Fact]
	public void EvalTimeout_IsReportedAsATimeout()
	{
		using var server = new FakeHotReplServer();
		server.Handler = FakeHotReplServer.EvalTimeoutFor;

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "state", "-TimeoutMs", "2000");

		Assert.Equal(3, run.ExitCode);
		Assert.True(run.Output.Contains("\"timeout\"", StringComparison.Ordinal), run.Output);
	}

	[Fact]
	public void MalformedOrTruncatedAnswers_AreDriverFailures()
	{
		using var malformed = new FakeHotReplServer();
		malformed.Handler = frame => FakeHotReplServer.ReplyFor(frame, "not-json");
		var malformedRun = DriverToolHarness.Run("-Url", malformed.Url, "-Action", "state");
		Assert.True(malformedRun.ExitCode == 4, $"a malformed answer is a driver failure, not exit {malformedRun.ExitCode}:{Environment.NewLine}{malformedRun.Output}");
		Assert.True(malformedRun.Output.Contains("driver-error", StringComparison.Ordinal), malformedRun.Output);

		using var truncated = new FakeHotReplServer();
		truncated.Handler = FakeHotReplServer.ReplyTruncated;
		var truncatedRun = DriverToolHarness.Run("-Url", truncated.Url, "-Action", "state");
		Assert.True(truncatedRun.ExitCode == 4, $"a truncated answer is a driver failure, not exit {truncatedRun.ExitCode}:{Environment.NewLine}{truncatedRun.Output}");
		Assert.True(truncatedRun.Output.Contains("truncated", StringComparison.Ordinal), truncatedRun.Output);
	}

	[Fact]
	public void ConnectionRefused_IsATransportFailure()
	{
		var port = FakeHotReplServer.FreePort();

		var run = DriverToolHarness.Run("-Url", $"ws://127.0.0.1:{port}/", "-Action", "state", "-TimeoutMs", "2000");

		Assert.Equal(2, run.ExitCode);
		Assert.True(run.Output.Contains("transport", StringComparison.Ordinal), run.Output);
	}

	[Fact]
	public void ASilentClient_TimesOut()
	{
		using var server = new FakeHotReplServer();
		server.Handler = _ => null;

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "state", "-TimeoutMs", "200");

		Assert.Equal(3, run.ExitCode);
		Assert.True(run.Output.Contains("timeout", StringComparison.Ordinal), run.Output);
	}

	[Fact]
	public void MissingOrMalformedParameters_AreUsageErrors()
	{
		Assert.Equal(64, DriverToolHarness.Run("-Action", "state").ExitCode);
		Assert.Equal(64, DriverToolHarness.Run("-Url", "ws://127.0.0.1:1/", "-Action", "click").ExitCode);
		Assert.Equal(64, DriverToolHarness.Run("-Url", "ws://127.0.0.1:1/", "-Action", "join-lobby", "-LobbyId", "not-a-number").ExitCode);
		Assert.Equal(64, DriverToolHarness.Run("-Url", "http://127.0.0.1:1/", "-Action", "state").ExitCode);
	}

	[Fact]
	public void Quit_AsksTheClientToQuit()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"quitting\":true}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "quit");

		Assert.Equal(0, run.ExitCode);
		Assert.Equal("true", DriverToolHarness.Field(run.Output, "quitting"));
		var frames = server.Frames;
		Assert.Single(frames);
		Assert.True(FakeHotReplServer.AsksFor(frames[0], "quit"), "quit is its own command, not an eval the caller writes");
	}

	[Fact]
	public void Quit_WithAnEvalErrorFails()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.EvalErrorFor(frame, "no quit here");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "quit");

		Assert.Equal(1, run.ExitCode);
		Assert.True(run.Output.Contains("eval-error", StringComparison.Ordinal), "a client that answers an error was not asked cleanly");
	}

	[Fact]
	public void Recipe_RunsOneCommittedRecipeAsOneEval()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"consciousness\":0,\"brainHealth\":1}");

		var run = DriverToolHarness.Run(
			"-Url", server.Url, "-Action", "recipe", "-Recipe", "body-force",
			"-RecipeArg", "consciousness=0,energy=-1,brainHealth=1,badSleepAmount=150,idleTime=-1");

		Assert.Equal(0, run.ExitCode);
		Assert.Equal("0", DriverToolHarness.Field(run.Output, "consciousness"));
		Assert.Equal("body-force", DriverToolHarness.Field(run.Output, "recipe"));

		var frame = Assert.Single(server.Frames);
		Assert.True(frame.Contains("var consciousness = 0;", StringComparison.Ordinal), "a number argument arrives as a C# number literal");
		Assert.True(frame.Contains("var badSleepAmount = 150;", StringComparison.Ordinal), "every declared argument is substituted");
		Assert.True(frame.Contains("PlayerCamera.main", StringComparison.Ordinal), "the frame carries the committed recipe, not a hand-rolled snippet");
		Assert.False(frame.Contains("{{", StringComparison.Ordinal), "every placeholder is substituted before the frame is sent");
	}

	[Fact]
	public void Recipe_MissingUnusedOrUnknownArgumentsAreUsageErrors()
	{
		using var server = new FakeHotReplServer();
		server.Handler = _ => null;

		var missing = DriverToolHarness.Run("-Url", server.Url, "-Action", "recipe", "-Recipe", "body-force", "-RecipeArg", "consciousness=0");
		Assert.Equal(64, missing.ExitCode);
		Assert.True(missing.Output.Contains("needs -RecipeArg", StringComparison.Ordinal), missing.Output);

		var unused = DriverToolHarness.Run("-Url", server.Url, "-Action", "recipe", "-Recipe", "body-read", "-RecipeArg", "consciousness=0");
		Assert.Equal(64, unused.ExitCode);
		Assert.True(unused.Output.Contains("is not used", StringComparison.Ordinal), unused.Output);

		var unknown = DriverToolHarness.Run("-Url", server.Url, "-Action", "recipe", "-Recipe", "not-a-recipe");
		Assert.Equal(64, unknown.ExitCode);
		Assert.True(unknown.Output.Contains("no recipe", StringComparison.Ordinal), unknown.Output);

		Assert.Empty(server.Frames);
	}

	[Fact]
	public void Recipe_ReportThatFailsIsADriverFailure()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":false,\"error\":\"no-target\",\"detail\":\"no other in-world member and no explicit target\"}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "recipe", "-Recipe", "carry-start", "-RecipeArg", "mode=carry,target=auto");

		Assert.Equal(1, run.ExitCode);
		Assert.True(run.Output.Contains("no-target", StringComparison.Ordinal), run.Output);
		Assert.Single(server.Frames);
	}

	[Fact]
	public void Recipe_ReportWithoutAnOkFieldIsADriverFailure()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"value\":1}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "recipe", "-Recipe", "body-read");

		Assert.Equal(1, run.ExitCode);
		Assert.True(run.Output.Contains("recipe-contract", StringComparison.Ordinal), run.Output);
	}

	[Fact]
	public void Recipe_WithAnEvalErrorFails()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.EvalErrorFor(frame, "recipe blew up");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "recipe", "-Recipe", "body-read");

		Assert.Equal(1, run.ExitCode);
		Assert.True(run.Output.Contains("eval-error", StringComparison.Ordinal), run.Output);
		Assert.True(run.Output.Contains("recipe blew up", StringComparison.Ordinal), "the client's message must reach the caller");
	}

	[Fact]
	public void TheConnectTimeoutFollowsTheActionBudget()
	{
		var script = File.ReadAllText(DriverToolHarness.FindScript());

		Assert.True(
			script.Contains("$connectTimeoutMs = [Math]::Min(10000, [Math]::Max(500, $TimeoutMs + 500))", StringComparison.Ordinal),
			"a fixed five-second connect cap reports a loaded machine's slow first connection as a timeout (observed in two full-suite runs: exit 3)");
	}

	[Fact]
	public void TheToolFiles_NeverReachForOsLevelInput()
	{
		var script = File.ReadAllText(DriverToolHarness.FindScript());
		var template = File.ReadAllText(DriverToolHarness.FindTemplate());

		foreach (var token in new[] { "SendKeys", "SendInput", "keybd_event", "mouse_event", "SetCursorPos", "GetCursorPos", "GetAsyncKeyState", "BlockInput", "AttachThreadInput", "SetForegroundWindow", "SetFocus", "SetWindowsHookEx", "SendMessage", "PostMessage", "Clipboard" })
		{
			Assert.False(script.Contains(token, StringComparison.Ordinal), $"the driver script uses an OS-level input API: {token}");
			Assert.False(template.Contains(token, StringComparison.Ordinal), $"the in-process template uses an OS-level input API: {token}");
		}

		Assert.True(script.Contains("driver\\InProcessDriver.cs", StringComparison.Ordinal), "the script must send the committed in-process half");
		Assert.True(template.Contains("{{COMMAND}}", StringComparison.Ordinal), "the template must keep its substitution contract");
	}
}
