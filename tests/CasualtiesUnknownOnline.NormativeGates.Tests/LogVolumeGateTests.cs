using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The log-volume gate (tickets <c>backlog/todo/layer-change-member-dropout.md</c> and
/// <c>backlog/review/remote-clone-warning-storm-on-member-dropout.md</c>; batches `20261005-b` and
/// `20261007-a`).
///
/// <para>
/// The first batch lost a session to a warning storm: after a layer change the two members were out of the world
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
/// The second batch found the family's third producer on the same real shape: with a member out of the world,
/// the renderer's lazy clone ensure retried once per frame, and the clone-creation failure line grew that
/// client's log by 7.25 MB/min (58,148 identical lines in ~90 seconds, still climbing at close). Same shape,
/// same level, one producer further out than the census reached.
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
/// emitters (<c>ItemPositionFollow.ReportDivergence</c>/<c>ReportSnap</c>, <c>LayerModifierSync.ApplyIndex</c>),
/// the 10 Hz receive handler (<c>FluidRegionHandler.Handle</c>), the clone-creation path
/// (<c>RemoteBodyFactory.CreateRemoteBody</c>) and the three places the renderer drains a failure run
/// (<c>EndCloneFailureRuns</c>, <c>FlushCloneFailures</c>, <c>ReportCloneFailureRun</c>) together with the
/// sites that call them, each resolved through Roslyn, plus a census floor over the whole of the FIVE files so
/// a renamed method or an emptied scan fails loudly instead of checking nothing. Every matcher is pinned with
/// positive and negative samples. What is NOT reached: the runtime volume — only a three-client run can measure
/// what the log actually grew by, and the next batch's row (a consecutive layer change with the members staying
/// in the world) is that run's — and the guard's own subject-list contract, which its unit tests own.
/// </para>
/// </summary>
public class LogVolumeGateTests
{
	private const string ItemFollowFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemPositionFollow.cs";

	private const string LayerModSyncFile = "src/CasualtiesUnknownOnline.GameAdapter/WorldGen/LayerModifierSync.cs";

	private const string FluidRegionHandlerFile = "src/CasualtiesUnknownOnline.Runtime/Session/Handlers/FluidRegionHandler.cs";

	private const string RemoteBodyFactoryFile = "src/CasualtiesUnknownOnline.GameAdapter/Character/RemoteBodyFactory.cs";

	private const string RemoteRendererFile = "src/CasualtiesUnknownOnline.GameAdapter/Character/RemotePlayerRenderer.cs";

	/// <summary>The bounded window a repeatable diagnostic asks before it writes a line.</summary>
	private const string WindowReport = "Report(";

	/// <summary>How many window lookups the item follow pump must keep: two `ShouldLog` and two `Repeated`.</summary>
	private const int MinimumItemFollowWindowSites = 4;

	/// <summary>How many window lookups the layer-mod sync must keep: both diagnostics plus their flushes.</summary>
	private const int MinimumLayerModWindowSites = 3;

	/// <summary>How many window lookups the clone-creation path must keep: one ask per failure it reports.</summary>
	private const int MinimumRemoteBodyWindowSites = 2;

	/// <summary>How many window lookups the renderer must keep: the drain that reports a run's swallowed lines.</summary>
	private const int MinimumRemoteRendererWindowSites = 1;

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

	/// <summary>
	/// The family's third producer (filed by batch `20261007-a`): the clone-creation failure line, written by a
	/// retry loop that runs once per member PER FRAME while the member has no render clone.
	/// </summary>
	[Fact]
	public void TheRemoteCloneFailureLines_AskTheWindowBeforeTheyWarn()
	{
		var create = RequireMethodBody(RemoteBodyFactoryFile, "RemoteBodyFactory", "CreateRemoteBody");

		Assert.True(
			EveryLogSitsInsideTheAsk(create),
			$"{RemoteBodyFactoryFile}: `CreateRemoteBody` is retried every frame for every member whose clone cannot be built, and its two failure lines (`no template`, `no Body`) do not resolve while the member stays out of the world — batch `20261007-a` measured this line unbounded at 7.25 MB/min (58,148 identical lines in ~90 seconds, 8.796 MB, still climbing at close). Both lines must ask a repetition window and be written INSIDE that ask, so one unchanged failure costs a bounded window of lines and its end still reports what the window swallowed");
		Assert.True(
			WindowSites(RepositoryPaths.ReadText(RemoteBodyFactoryFile)) >= MinimumRemoteBodyWindowSites,
			$"{RemoteBodyFactoryFile}: both failure lines must still ask the window (census floor {MinimumRemoteBodyWindowSites}) — a renamed or deleted ask means this gate's scan surface is stale, not that the log is bounded");
	}

	/// <summary>
	/// The other half of the producer's contract: the lines a window refused are counted and REPORTED where the
	/// run ends, and a run still open when the session ends is reported too — which is the storm's own case, its
	/// condition never having resolved before the client closed.
	/// </summary>
	[Fact]
	public void TheRemoteCloneFailureRuns_AreDrainedWhereTheyEnd()
	{
		var report = RequireMethodBody(RemoteRendererFile, "RemotePlayerRenderer", "ReportCloneFailureRun");
		var end = RequireMethodBody(RemoteRendererFile, "RemotePlayerRenderer", "EndCloneFailureRuns");
		var windDown = RequireMethodBody(RemoteRendererFile, "RemotePlayerRenderer", "FlushCloneFailures");
		var pump = RequireMethodBody(RemoteRendererFile, "RemotePlayerRenderer", "Update");
		var sceneChange = RequireMethodBody(RemoteRendererFile, "RemotePlayerRenderer", "OnRemoteSceneChanged");
		var teardown = RequireMethodBody(RemoteRendererFile, "RemotePlayerRenderer", "DestroyAllClones");

		Assert.True(
			EveryLogSitsInsideTheDrain(report) && Called(report, "LogWarning"),
			$"{RemoteRendererFile}: a failure run that ends must report what its window swallowed, at Warning, and the line must SIT inside the drain's own ask — a drain whose log left its `if` would warn on every clone the renderer builds again, which is the storm one level down");
		Assert.True(
			CallSites(end, "ReportCloneFailureRun") >= 2,
			$"{RemoteRendererFile}: BOTH failures' runs end with the member (the scene may have no template, or its template may clone without a Body), so both subjects report");
		Assert.True(
			ReadsMember(windDown, "Subjects") && Called(windDown, "ReportCloneFailureRun"),
			$"{RemoteRendererFile}: a run still open when the session ends must be drained the same way — the wind-down walks `LogRepetitionGuard.Subjects` and reports each subject through the same drain, and for batch `20261007-a`'s storm that walk is the ONLY path that could ever tell its size (nothing else ended the run)");
		Assert.True(
			Called(pump, "EndCloneFailureRuns"),
			$"{RemoteRendererFile}: the run ends where the member starts drawing a clone again (`Update`) — without that call the drain is dead code and the swallowed lines stay untold");
		Assert.True(
			Called(sceneChange, "EndCloneFailureRuns"),
			$"{RemoteRendererFile}: a member out of the world is attempted no more, so its run ends at the scene change — and a failure that comes back with it reports its first line instead of being refused as a repeat of the run that ended");
		Assert.True(
			Called(teardown, "FlushCloneFailures"),
			$"{RemoteRendererFile}: the teardown reports every run still open before the window is forgotten — batch `20261007-a`'s storm is the case where nothing else ever ends the run");
		Assert.True(
			WindowSites(RepositoryPaths.ReadText(RemoteRendererFile)) >= MinimumRemoteRendererWindowSites,
			$"{RemoteRendererFile}: the drain must still ask the window (census floor {MinimumRemoteRendererWindowSites}) — a renamed drain means this gate's scan surface is stale, not that the bound reports");
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
	[InlineData("if (_windows.TryLog(kind, itemId, out _)) { } _distanceLog.Repeated(kind, itemId, distance); _log.LogInformation(\"settle\");", false)]
	public void TheLineMatcher_RequiresTheLogToBeGatedByTheWindow(string body, bool expected) =>
		Assert.True(expected == EveryLineGoesThroughTheWindow(body), $"a correction line is bounded only when its own log asks the window in an `if` AND counts what the window refused — and the ask must be the ITEM lines' own `ShouldLog`, so the family's other spelling cannot make an ungated line pass here: {body}");

	[Theory]
	[InlineData("if (failures.TryLog(key, null, out var repeat)) { _log.LogWarning(\"Remote body: {Why} for {SteamId} (repeat {Repeat}).\", why, steamId, repeat); } return null;", true)]
	[InlineData("_log.LogWarning(\"Remote body: no Body component in \\\"Experiment\\\" clone.\"); return null;", false)]
	[InlineData("if (failures.TryLog(key, null, out var repeat)) { return null; } _log.LogWarning(\"Remote body: {Why} for {SteamId}.\", why, steamId);", false)]
	[InlineData("if (failures.TryLog(key, null, out var repeat)) { } _log.LogWarning(\"Remote body: {Why} for {SteamId}.\", why, steamId);", false)]
	[InlineData("_log.LogInformation(\"Remote body created for {SteamId}.\", steamId);", false)]
	public void TheAskMatcher_RequiresEveryLogInsideTheAsk(string body, bool expected) =>
		Assert.True(expected == EveryLogSitsInsideTheAsk(body), $"a line is bounded only when it SITS inside the ask that refuses its repeats — the calls being present while the log is outside them is the shape that reads like a bound and is not one: {body}");

	[Theory]
	[InlineData("if (_cloneFailures.TryFlush(subject, out var suppressed)) { _log.LogWarning(\"Remote body: {Why} — {Count} suppressed.\", subject.Why, suppressed); }", true)]
	[InlineData("if (_cloneFailures.TryLog(key, null, out var repeat)) { _log.LogWarning(\"Remote body: {Why} (repeat {Repeat}).\", key, repeat); }", false)]
	[InlineData("_cloneFailures.TryFlush(subject, out var suppressed); _log.LogWarning(\"Remote body: {Why} — {Count} suppressed.\", subject.Why, suppressed);", false)]
	public void TheDrainMatcher_RequiresEveryLogInsideTheDrain(string body, bool expected) =>
		Assert.True(expected == EveryLogSitsInsideTheDrain(body), $"a run's end is reported only when its line SITS inside the drain that hands the count back — ASKING a window is not draining one: {body}");

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
	/// True when every <c>LogInformation</c> in the body is GATED by the window: it asks the item lines'
	/// own <c>ShouldLog</c> in an <c>if</c> condition and counts refusals with <c>Repeated</c> — the
	/// early-return guard clause `if (!ShouldLog(...)) { Repeated(...); return; }` followed by the line.
	/// Written this way because the shape it guards against is exactly "the calls are still there, the log is
	/// not behind them" — the gate's own summary says a diagnostic must ask the window BEFORE it logs, and
	/// "before" is a call order, not a mention. Its ask spelling is deliberately ITS OWN
	/// (<see cref="AsksTheDistanceWindow"/>): the family's second spelling belongs to
	/// <see cref="EveryLogSitsInsideTheAsk"/>, and a body that asks `TryLog` and then writes its line outside
	/// that ask must fail HERE rather than ride the family's widening.
	/// </summary>
	private static bool EveryLineGoesThroughTheWindow(string body)
	{
		var root = Parse(body);
		var asks = root.DescendantNodes().OfType<IfStatementSyntax>().Any(AsksTheDistanceWindow);
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

	/// <summary>
	/// True when every log line a body writes SITS INSIDE the branch whose condition asks the window. This is
	/// the family's second shape — the positive gate (<c>if (TryLog(...)) { log(...); }</c>, the one
	/// <c>LayerModifierSync</c> uses) — where bounding is containment; <see cref="EveryLineGoesThroughTheWindow"/>
	/// pins the first shape, the early-return guard clause, which bounds by returning before the line. Both are
	/// written as syntax rather than as a mention because "the window call is there and the log is not behind it"
	/// is precisely the regression a census of call sites cannot see.
	/// </summary>
	private static bool EveryLogSitsInsideTheAsk(string body) => EveryLogSitsInsideTheWindowBranch(body, "ShouldLog", "TryLog");

	/// <summary>
	/// True when every log line a body writes SITS INSIDE the branch that DRAINS the window (<c>TryFlush</c>) —
	/// the summary a run's end writes. Its own spelling, like the ask's: a drain whose line left its `if` would
	/// report on every call instead of on the runs that cost lines.
	/// </summary>
	private static bool EveryLogSitsInsideTheDrain(string body) => EveryLogSitsInsideTheWindowBranch(body, "TryFlush");

	private static bool EveryLogSitsInsideTheWindowBranch(string body, params string[] gates)
	{
		var root = Parse(body);
		var branches = root.DescendantNodes()
			.OfType<IfStatementSyntax>()
			.Where(branch => branch.Condition.DescendantNodesAndSelf()
				.OfType<InvocationExpressionSyntax>()
				.Any(invocation => gates.Any(gate => Called(invocation, gate))))
			.ToList();
		var lines = LogCalls(root).ToList();

		// A body with no line at all is not a gated line: it is a helper, or the scan surface moved, and the
		// caller's own assertion is what says the line must exist.
		return lines.Count > 0 && lines.All(line => branches.Any(branch => branch.Statement.Span.Contains(line.Span)));
	}

	/// <summary>The lines a repeatable diagnostic writes at a level a player's rolling log pays for.</summary>
	private static IEnumerable<InvocationExpressionSyntax> LogCalls(SyntaxNode root) =>
		root.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Where(invocation => Called(invocation, "LogInformation") || Called(invocation, "LogWarning"));

	/// <summary>
	/// True when this branch's condition asks the ITEM lines' own window (<c>ShouldLog</c>). Held apart from the
	/// family's other spelling on purpose: the two matchers must widen independently, or a body asking one window
	/// and writing its line outside that ask would pass. <c>DescendantNodesAndSelf</c>: in the positive-gate shape
	/// the condition IS the call (<c>if (TryLog(...))</c>), while here it wraps one (<c>if (!ShouldLog(...))</c>).
	/// </summary>
	private static bool AsksTheDistanceWindow(IfStatementSyntax branch) => AsksTheWindowWith(branch, "ShouldLog");

	private static bool AsksTheWindowWith(IfStatementSyntax branch, params string[] names) =>
		branch.Condition.DescendantNodesAndSelf()
			.OfType<InvocationExpressionSyntax>()
			.Any(invocation => names.Any(name => Called(invocation, name)));

	/// <summary>True when the body READS the named member (a property or field of something) — never a comment or a string literal.</summary>
	private static bool ReadsMember(string body, string name) =>
		Parse(body).DescendantNodes()
			.OfType<MemberAccessExpressionSyntax>()
			.Any(access => access.Name.Identifier.ValueText == name);

	/// <summary>True when the body contains a CALL of the named method (never a comment or a string literal).</summary>
	private static bool Called(string body, string name) => CallSites(body, name) > 0;

	/// <summary>How many CALLS of the named method a body contains — a call site is an invocation, not a mention.</summary>
	private static int CallSites(string body, string name) =>
		Parse(body).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Count(invocation => Called(invocation, name));

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
