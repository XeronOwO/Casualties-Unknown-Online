using System;
using System.IO;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// The black-box contract of the acceptance suite's <c>drive</c> subcommand (the port of the deleted
/// tools/acceptance/drive-in-process.ps1): one process per call against a fake evaluator endpoint, so
/// what the tool sends, and how it reports a missing client, a refusal, a compile error or a bad
/// argument, is pinned without a running game. The per-action sequences themselves are exercised by
/// the acceptance cases on a real pair; this file pins the tool's own edges.
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
		foreach (var action in new[] { "ping", "state", "open-window", "goto-page", "click", "set-text", "create-lobby", "join-lobby", "start-run", "continue-run", "leave-world", "console", "quit", "recipe", "declare" })
		{
			Assert.True(run.Output.Contains(action, StringComparison.Ordinal), $"the vocabulary does not name '{action}':{Environment.NewLine}{run.Output}");
		}
	}

	[Fact]
	public void Ping_SendsOneEvalOfTheCommittedTemplateAndReturnsTheClientsAnswer()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"command\":\"ping\",\"plugin\":true,\"overlay\":true,\"steamInit\":true,\"controlCount\":3}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "ping");

		Assert.Equal(0, run.ExitCode);
		Assert.Equal("true", DriverToolHarness.Field(run.Output, "overlay"));
		var frames = server.Frames;
		Assert.Single(frames);
		Assert.True(FakeHotReplServer.AsksFor(frames[0], "ping"), "ping is its own verb");
		Assert.True(frames[0].Contains("CasualtiesUnknownOnline.Runtime.CuoBootstrap", StringComparison.Ordinal), "the template must be the committed in-process half, not a hand-rolled snippet");
		Assert.False(frames[0].Contains("{{", StringComparison.Ordinal), "every placeholder must be substituted before the frame is sent");
	}

	[Fact]
	public void State_ReturnsTheClientsAnswerAndDerivesTheEvalTimeoutFromTheBudget()
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
		Assert.True(frames[0].Contains("\"timeoutMs\":10000", StringComparison.Ordinal), "the eval timeout is derived from the action budget (min 10 s here)");
	}

	[Fact]
	public void ARefusalAnswer_IsADriverFailure()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":false,\"error\":\"no-cuo-online-ui\",\"detail\":\"the evaluator answered but CUO is not here\"}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "ping");

		Assert.Equal(1, run.ExitCode);
		Assert.True(run.Output.Contains("no-cuo-online-ui", StringComparison.Ordinal), "the refusal's own code must reach the caller");
	}

	[Fact]
	public void EvalError_IsReportedAsADriverFailure()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.EvalErrorFor(frame, "boom");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "state");

		Assert.Equal(1, run.ExitCode);
		Assert.True(run.Output.Contains("boom", StringComparison.Ordinal), run.Output);
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
	public void ASilentClient_TimesOut()
	{
		using var server = new FakeHotReplServer();
		server.Handler = _ => null;

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "state", "-TimeoutMs", "600");

		Assert.Equal(3, run.ExitCode);
	}

	[Fact]
	public void ConnectionRefused_IsATransportFailure()
	{
		var port = FakeHotReplServer.FreePort();

		var run = DriverToolHarness.Run("-Url", $"ws://127.0.0.1:{port}/", "-Action", "state", "-TimeoutMs", "15000");

		Assert.True(run.ExitCode == 2, $"a refused connection is a transport failure, not {run.ExitCode}:{Environment.NewLine}{run.Output}");
	}

	[Fact]
	public void MissingOrMalformedParameters_AreUsageErrors()
	{
		Assert.Equal(64, DriverToolHarness.Run("-Url", "ws://127.0.0.1:1/").ExitCode);
		Assert.Equal(64, DriverToolHarness.Run("-Action", "state").ExitCode);
		Assert.Equal(64, DriverToolHarness.Run("-Url", "ws://127.0.0.1:1/", "-Action", "recipe").ExitCode);
		Assert.Equal(64, DriverToolHarness.Run("-Url", "ws://127.0.0.1:1/", "-Action", "declare").ExitCode);
	}

	[Fact]
	public void Recipe_OneFrameCarriesTheNameAndItsArguments()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"result\":\"bandage\"}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "recipe", "-Recipe", "limb-read", "-RecipeArg", "limb=2", "-RecipeArg", "source=local", "-RecipeArg", "target=0");

		Assert.Equal(0, run.ExitCode);
		Assert.Equal("bandage", DriverToolHarness.Field(run.Output, "result"));
		var frames = server.Frames;
		Assert.Single(frames);
		Assert.True(frames[0].Contains("limb-read", StringComparison.Ordinal), "the recipe's name must reach the snippet");
		Assert.True(frames[0].Contains("local", StringComparison.Ordinal), "the recipe's arguments must reach the snippet");
	}

	[Fact]
	public void Recipe_MissingArgument_IsAUsageError()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":true}");

		var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "recipe", "-Recipe", "carry-start");

		Assert.Equal(64, run.ExitCode);
		Assert.True(run.Output.Contains("needs an argument", StringComparison.Ordinal), run.Output);
	}

	[Fact]
	public void JsonPath_WritesTheAnswerFile()
	{
		using var server = new FakeHotReplServer();
		server.Handler = frame => FakeHotReplServer.ReplyFor(frame, "{\"ok\":true,\"lobby\":\"4242\"}");
		var path = Path.Combine(Path.GetTempPath(), "cuo-drive-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			var run = DriverToolHarness.Run("-Url", server.Url, "-Action", "state", "-JsonPath", path);

			Assert.Equal(0, run.ExitCode);
			Assert.True(File.Exists(path), "the answer file must exist");
			Assert.Contains("4242", File.ReadAllText(path), StringComparison.Ordinal);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void TheActionBudgetCancelsTheWholeAction()
	{
		var command = File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "CasualtiesUnknownOnline.AcceptanceSuite", "DriveCommand.cs"));

		Assert.True(
			command.Contains("using var cancellation = new CancellationTokenSource(timeoutMs);", StringComparison.Ordinal),
			"the whole action, its connect included, must be under the action budget: a fixed connect cap reports a loaded machine's slow first connection as a timeout");
		Assert.True(
			command.Contains("cancellation.Token", StringComparison.Ordinal),
			"the budget must reach the driver call, not just the argument parse");
	}

	[Fact]
	public void TheToolFiles_NeverReachForOsLevelInput()
	{
		var command = File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "CasualtiesUnknownOnline.AcceptanceSuite", "DriveCommand.cs"));
		var template = File.ReadAllText(DriverToolHarness.FindTemplate());

		foreach (var token in new[] { "SendKeys", "SendInput", "keybd_event", "mouse_event", "SetCursorPos", "GetCursorPos", "GetAsyncKeyState", "BlockInput", "AttachThreadInput", "SetForegroundWindow", "SetFocus", "SetWindowsHookEx", "SendMessage", "PostMessage", "Clipboard" })
		{
			Assert.False(command.Contains(token, StringComparison.Ordinal), $"the drive command uses an OS-level input API: {token}");
			Assert.False(template.Contains(token, StringComparison.Ordinal), $"the in-process template uses an OS-level input API: {token}");
		}

		Assert.True(template.Contains("{{COMMAND}}", StringComparison.Ordinal), "the template must keep its substitution contract");
	}

	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new InvalidOperationException($"no repository root above {AppContext.BaseDirectory}");
	}
}
