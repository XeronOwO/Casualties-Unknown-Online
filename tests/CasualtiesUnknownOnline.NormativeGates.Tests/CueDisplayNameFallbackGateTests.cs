using System;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The drag cue's blank-name fallback (ticket <c>docs/backlog/review/cross-player-drag-use-feedback.md</c> row 6): the
/// label formats <c>cue.use_on</c> with the Online UI's own display-name resolution, and that resolution ends in
/// <c>player-{id}</c> — never a dangling sentence. A blank Steam persona cannot be staged on a live session (the
/// persona is the account's own name), so the shape is pinned here instead of in a batch.
///
/// <para>
/// Reach, stated rather than implied: this reads SOURCE text of the three files the label path runs through —
/// <c>PluginHost.DrawDragUseCue</c> (the call site), <c>OnlineUiHost.DisplayName</c> (the no-context fallback) and
/// <c>OnlineUiContext.DisplayName</c> (the frame's own resolution) — and asks for the two facts the row turns on: the
/// sentence is formatted from the shared resolution, not from a second lookup, and both fallbacks carry the player id.
/// What it does NOT prove: that the label renders well, or that an IP-direct session with a blank name reaches it —
/// those stay the row's named limits.
/// </para>
/// </summary>
public sealed class CueDisplayNameFallbackGateTests
{
	private const string PluginHostFile = "src/CasualtiesUnknownOnline.Plugin/PluginHost.cs";
	private const string OnlineUiHostFile = "src/CasualtiesUnknownOnline.Plugin/OnlineUiHost.cs";
	private const string OnlineUiContextFile = "src/CasualtiesUnknownOnline.Plugin/OnlineUiContext.cs";

	[Fact]
	public void TheCueSentence_IsFormattedWithTheSharedNameResolution()
	{
		var body = RepositoryPaths.ReadText(PluginHostFile);

		Assert.True(
			body.Contains("cue.use_on", StringComparison.Ordinal),
			$"{PluginHostFile}: the drag cue no longer formats `cue.use_on` — row 6's sentence has no key");
		Assert.True(
			body.Contains("_onlineUi.DisplayName(cue.TargetSteamId)", StringComparison.Ordinal),
			$"{PluginHostFile}: the drag cue no longer asks the Online UI's own resolution — the label and the roster can disagree");
	}

	[Fact]
	public void TheNameResolution_FallsBackToTheStablePlayerId()
	{
		var contextBody = RepositoryPaths.ReadText(OnlineUiContextFile);
		Assert.True(
			contextBody.Contains("\"player-{id:X}\"", StringComparison.Ordinal),
			$"{OnlineUiContextFile}: a blank name no longer falls back to `player-{{id}}` — the cue would render a dangling sentence");

		var hostBody = RepositoryPaths.ReadText(OnlineUiHostFile);
		Assert.True(
			hostBody.Contains("\"player-{steamId:X}\"", StringComparison.Ordinal),
			$"{OnlineUiHostFile}: the no-context fallback is gone — the cue would show an empty name when no frame is built");
	}
}
