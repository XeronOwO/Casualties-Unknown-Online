using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The log-volume gate (ticket <c>backlog/todo/layer-change-member-dropout.md</c>, batch `20261005-b`).
///
/// <para>
/// That batch lost a session to a warning storm: after a layer change the two members were out of the world
/// and the guest's rolling log grew from 0.8 MB to 33.4 MB in about four minutes. The batch attributed the
/// growth to the repeating <c>[LayerMod] baseline divergence</c> warning. Measured against the surviving
/// client logs, that warning is the smaller half: it is emitted once per arriving snapshot (a 100 ms stream),
/// while the volume is dominated by <c>[ItemPhysics] settle</c> — one Information line per item PER FRAME
/// while a copy's gap to the host's state does not close, which is exactly what a diverged world looks like.
/// Two defects of one shape: a diagnostic whose trigger frequency is per-frame or per-snapshot, written at a
/// level meant for low-frequency events, with no bound on its repetition.
/// </para>
///
/// <para>
/// The rule this gate encodes: a diagnostic that can repeat on every frame or on every arriving snapshot must
/// ask a repetition window (<c>LogRepetitionGuard</c> / <c>ItemDistanceLog</c>) before it logs, so one
/// unchanged fact costs a bounded number of lines and its end still reports what the window swallowed. The
/// window's own behaviour is pinned by <c>LogRepetitionGuardTests</c>; this gate pins WHERE it is asked.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the scan surface is the bodies of the two per-frame/per-snapshot
/// emitters (<c>ItemPositionFollow.ReportDivergence</c>/<c>ReportSnap</c>, <c>LayerModifierSync.ApplyIndex</c>)
/// and the 10 Hz receive handler (<c>FluidRegionHandler.Handle</c>), each resolved through Roslyn, plus a
/// census floor over the whole of the two files so a renamed method or an emptied scan fails loudly instead of
/// checking nothing. Every matcher is pinned with positive and negative samples. What is NOT reached: the
/// runtime volume — only a three-client run can measure what the log actually grew by, and the ticket's
/// runtime row (a consecutive layer change with the members staying in the world) is that run's.
/// </para>
/// </summary>
public class LogVolumeGateTests
{
	private const string ItemFollowFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemPositionFollow.cs";

	private const string LayerModSyncFile = "src/CasualtiesUnknownOnline.GameAdapter/WorldGen/LayerModifierSync.cs";

	private const string FluidRegionHandlerFile = "src/CasualtiesUnknownOnline.Runtime/Session/Handlers/FluidRegionHandler.cs";

	/// <summary>The bounded window a repeatable diagnostic asks before it writes a line.</summary>
	private const string WindowReport = "Report(";

	/// <summary>How many window lookups the item follow pump must keep: two `ShouldLog` and two `Repeated`.</summary>
	private const int MinimumItemFollowWindowSites = 4;

	/// <summary>How many window lookups the layer-mod sync must keep: both diagnostics plus their flushes.</summary>
	private const int MinimumLayerModWindowSites = 3;

	[Fact]
	public void TheItemFollowCorrectionLines_AskTheWindowBeforeTheyLog()
	{
		var settle = RequireMethodBody(ItemFollowFile, "ItemPositionFollow", "ReportDivergence");
		var snap = RequireMethodBody(ItemFollowFile, "ItemPositionFollow", "ReportSnap");

		Assert.True(
			EveryLineGoesThroughTheWindow(settle),
			$"{ItemFollowFile}: `ReportDivergence` must ask the repetition window (`ShouldLog`) before its `LogInformation` and count what the window refused (`Repeated`) — batch `20261005-b`'s storm was this line, per item per frame, and an unbounded line is what grew a 0.8 MB log to 33.4 MB in four minutes");
		Assert.True(
			EveryLineGoesThroughTheWindow(snap),
			$"{ItemFollowFile}: `ReportSnap` must ask the same window — a copy a diverged world keeps pushing past the snap threshold writes this line every frame too, and it is the settle line's sibling in every respect");
		Assert.True(
			WindowSites(RepositoryPaths.ReadText(ItemFollowFile)) >= MinimumItemFollowWindowSites,
			$"{ItemFollowFile}: the two per-frame correction lines must both still exist behind the window (census floor {MinimumItemFollowWindowSites}) — a renamed or deleted emitter means this gate's scan surface is stale, not that the log is bounded");
	}

	[Fact]
	public void TheLayerModifierDiagnostics_AskTheirWindowsAndFlushWhatTheySwallowed()
	{
		var apply = RequireMethodBody(LayerModSyncFile, "LayerModifierSync", "ApplyIndex");
		var flush = RequireMethodBody(LayerModSyncFile, "LayerModifierSync", "FlushRepeat");

		Assert.True(
			WindowSites(apply) >= 2,
			$"{LayerModSyncFile}: `ApplyIndex` must ask a window for BOTH diagnostics (the index disagreement and the baseline divergence) — each is emitted for its arriving snapshot, and a standing baseline pair repeats identically on every one of them");
		Assert.True(
			WindowSites(flush) >= 1,
			$"{LayerModSyncFile}: `FlushRepeat` must be the one place the window is drained, so the lines a standing divergence cost are counted and reported instead of lost — a bound that swallows silently would hide the divergence the detector exists to raise");
		Assert.True(
			WindowSites(RepositoryPaths.ReadText(LayerModSyncFile)) >= MinimumLayerModWindowSites,
			$"{LayerModSyncFile}: both diagnostics plus the flush must still ask the window (census floor {MinimumLayerModWindowSites}) — a renamed guard means this gate's scan surface is stale, not that the warning is bounded");
	}

	[Fact]
	public void TheTenHertzFluidRegionHandler_BoundsItsReceiveLog()
	{
		var handle = RequireMethodBody(FluidRegionHandlerFile, "FluidRegionHandler", "Handle");

		Assert.True(
			WindowSites(handle) >= 1,
			$"{FluidRegionHandlerFile}: `Handle` must ask a repetition window — the fluid region stream runs at 10 Hz (`AdaptiveStreamId.FluidRegionDiffStream`), so an Information line per arriving region is a per-tick log, which the level policy puts at Verbose/Debug");
		Assert.True(
			Called(handle, "LogDebug"),
			$"{FluidRegionHandlerFile}: the repeating region must fall back to Debug — bounding the Information line without a Debug path would make the stream's per-region detail unobservable instead of quiet (the sender, `FluidSimulationAuthority`, logs this stream at Debug for the same reason)");
	}

	[Theory]
	[InlineData("var entry = _windows.ShouldLog(ItemDistanceLog.Kind.Settle, itemId, distance, out var repeat); _log.LogInformation(\"[ItemPhysics] settle {Id}.\", itemId, repeat);", true)]
	[InlineData("_windows.Repeated(ItemDistanceLog.Kind.Snap, itemId, distance);", true)]
	[InlineData("_log.LogInformation(\"[ItemPhysics] settle {Id} d={Dist:F2}.\", itemId, d.Dist);", false)]
	[InlineData("// the window is asked in ShouldLog elsewhere", false)]
	public void TheWindowMatcher_ReadsACallAndNotAMention(string body, bool expected) =>
		Assert.True(expected == WindowSites(body) >= 1, $"the window matcher must read a CALL (`{WindowReport}`) and never a comment or a string literal: {body}");

	[Theory]
	[InlineData("if (!_distanceLog.ShouldLog(kind, itemId, distance, out var repeat)) { _distanceLog.Repeated(kind, itemId, distance); return false; } _log.LogInformation(\"settle\");", true)]
	[InlineData("_log.LogInformation(\"[ItemPhysics] settle {Id} d={Dist:F2}.\", key, d.Dist);", false)]
	[InlineData("_log.LogInformation(\"settle\"); _distanceLog.ShouldLog(kind, itemId, distance, out _); _distanceLog.Repeated(kind, itemId, distance);", false)]
	[InlineData("_log.LogWarning(\"[LayerMod] baseline divergence.\", local, host);", false)]
	public void TheLineMatcher_RequiresTheLogToBeGatedByTheWindow(string body, bool expected) =>
		Assert.True(expected == EveryLineGoesThroughTheWindow(body), $"a correction line is bounded only when its own log asks the window in an `if` AND counts what the window refused: {body}");

	[Theory]
	[InlineData("_log.LogDebug(\"[Fluid] region.\");", "LogDebug", true)]
	[InlineData("// the Debug path used to be LogDebug( here", "LogDebug", false)]
	[InlineData("_regions.TryLog(\"key\", \"value\", out var repeat);", "LogDebug", false)]
	public void TheCallMatcher_ReadsACallAndNotAComment(string body, string name, bool expected) =>
		Assert.True(expected == Called(body, name), $"the call matcher must read an invocation of `{name}` and never a comment that mentions it: {body}");

	/// <summary>Window lookups in a source, read by CALL so a comment or a string literal is not a site.</summary>
	private static int WindowSites(string source) =>
		Parse(source).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Count(invocation => invocation.Expression switch
			{
				MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText is "TryLog" or "ShouldLog" or "Repeated" or "TryFlush",
				IdentifierNameSyntax identifier => identifier.Identifier.ValueText is "TryLog" or "ShouldLog",
				_ => false,
			});

	/// <summary>
	/// True when every <c>LogInformation</c> in the body is GATED by the window: it asks <c>ShouldLog</c> in an
	/// <c>if</c> condition and counts refusals with <c>Repeated</c>. Written this way because the shape it guards
	/// against is exactly "the calls are still there, the log is not behind them" — the gate's own summary says a
	/// diagnostic must ask the window BEFORE it logs, and "before" is a call order, not a mention.
	/// </summary>
	private static bool EveryLineGoesThroughTheWindow(string body)
	{
		var root = Parse(body);
		var asks = root.DescendantNodes()
			.OfType<IfStatementSyntax>()
			.Any(branch => branch.Condition.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => Called(invocation, "ShouldLog")));
		var counts = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => Called(invocation, "Repeated"));
		var logs = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation => Called(invocation, "LogInformation"));

		// A body with no log line at all is not a correction line: it is a helper, and this matcher is about
		// the log being gated, so the caller's own assertion is what says the line must exist.
		return logs && asks && counts;
	}

	private static bool Called(InvocationExpressionSyntax invocation, string name) =>
		invocation.Expression switch
		{
			MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == name,
			IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
			_ => false,
		};

	/// <summary>True when the body contains a CALL of the named method (never a comment or a string literal).</summary>
	private static bool Called(string body, string name) =>
		Parse(body).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Any(invocation => Called(invocation, name));

	/// <summary>The body text of a method declared in one file, or a failed assertion naming the stale scan surface.</summary>
	private static string RequireMethodBody(string file, string typeName, string methodName)
	{
		var type = Parse(RepositoryPaths.ReadText(file)).DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.FirstOrDefault(declaration => string.Equals(declaration.Identifier.ValueText, typeName, StringComparison.Ordinal));
		Assert.True(type is not null, $"{file} no longer declares `{typeName}` — this gate's scan surface is stale, not clean");

		var method = type!.Members.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(candidate => string.Equals(candidate.Identifier.ValueText, methodName, StringComparison.Ordinal));
		Assert.True(method is not null, $"{file}: `{typeName}` no longer declares `{methodName}` — this gate's scan surface is stale, not clean");

		return method!.Body is not null ? method.Body.ToString() : method.ExpressionBody?.ToString() ?? string.Empty;
	}

	private static SyntaxNode Parse(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
}
