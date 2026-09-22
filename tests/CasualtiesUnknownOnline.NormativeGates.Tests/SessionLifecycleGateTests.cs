using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Gate for the session-teardown contract (<c>ISessionReset</c>): a service that owns
/// state belonging to a session reacts to <c>ISessionControl.SessionEnded</c> through
/// the ONE contract method (<c>ResetSessionState</c>), the containing type declares
/// the contract, every method of that name lives in a type that declares it, and every
/// subscription on the session's lifecycle events carries its unbind half in the same
/// file. Before the contract the same stage was spelled five ways (the bare reset, two
/// session-suffixed variants, the event-handler name and a bare <c>ResetSession</c>
/// helper) and nothing asserted that a session-scoped service had one at all.
///
/// <para>
/// Scan surface and its boundary, stated rather than implied. The scan covers the
/// framework layers that own CUO services — <c>src/CasualtiesUnknownOnline.Runtime</c>,
/// <c>src/CasualtiesUnknownOnline.Application</c> and
/// <c>src/CasualtiesUnknownOnline.GameAdapter</c>. The example mod
/// (<c>src/CasualtiesUnknownOnline.ModExample</c>) is a consumer of the mod API (one
/// one-shot lambda on the mod context's own event) and is outside the rule. The
/// Application layer cannot implement the Runtime contract — project direction allows
/// GameState and Protocol only — so its reset surface is a DECLARED exemption list,
/// asserted for completeness and for staleness. The Game Adapter wires its domains
/// through its session binding's Bind/Unbind pairs; its session-end subscribers are a
/// declared census, and its domains' handlers are the adapter's own.
/// </para>
///
/// <para>
/// Known blind spots, named here so they are boundaries rather than surprises: (1) a
/// service that owns session state and neither subscribes to the edge nor declares the
/// contract is invisible to a source scan — the teeth are on the reaction path and on
/// the declared name; (2) <c>+= value</c> event-accessor forwards
/// (<c>SessionService</c>'s own surface) are skipped, because an add/remove forward is
/// inherently paired; (3) the declaration check reads the file's type-declaration
/// heads, so a NESTED type is validated against its file (the one-top-level-type gate
/// bounds the rest); (4) comment stripping is textual, so a <c>//</c> inside a string
/// literal truncates the rest of that line for the scan; (5) pairing is file-scoped, so
/// a <c>-=</c> in another type of the same file would satisfy it; (6) the scan proves
/// shape, not that the reset body is correct.
/// </para>
/// </summary>
public class SessionLifecycleGateTests
{
	private const string ContractPath = "src/CasualtiesUnknownOnline.Runtime/Session/ISessionReset.cs";
	private const string ContractMethod = "ResetSessionState";
	private const string ContractInterface = "ISessionReset";
	private const string RuntimeRoot = "src/CasualtiesUnknownOnline.Runtime/";
	private const string ApplicationRoot = "src/CasualtiesUnknownOnline.Application/";
	private const string GameAdapterRoot = "src/CasualtiesUnknownOnline.GameAdapter/";

	/// <summary>Census floor: the Runtime carries twenty session-end subscribers (measured 2026-09-22, the composition-modules cycle).</summary>
	private const int RuntimeSessionEndFloor = 18;

	/// <summary>Census floor: the scanned framework layers carry 94 session-lifecycle subscriptions (measured 2026-09-22: Runtime 66, Application 2, Game Adapter 26); the floor is about 70 % of that surface.</summary>
	private const int FrameworkSubscriptionFloor = 66;

	/// <summary>Declared census: the Game Adapter's session-end subscribers (its session binding plus the three domains that bind the edge themselves); its domains are torn down by the binding, so the number is a review decision, not a scan artifact.</summary>
	private const int GameAdapterSessionEndCensus = 4;

	/// <summary>Census floor: Runtime + Application declare 32 methods named <c>ResetSessionState</c> (measured 2026-09-22: Runtime 29 — the interface's own declaration included — Application 3); the floor is about 70 %.</summary>
	private const int ResetMethodFloor = 22;

	/// <summary>Census floor: <c>src/</c> holds 1,635 C# files (measured 2026-09-22); the floor is a little over half of it.</summary>
	private const int SourceFileFloor = 900;

	private static readonly string[] ScannedRoots =
	[
		"src/CasualtiesUnknownOnline.Runtime",
		"src/CasualtiesUnknownOnline.Application",
		"src/CasualtiesUnknownOnline.GameAdapter"
	];

	/// <summary>
	/// The Application layer cannot implement the Runtime contract (it may reference
	/// GameState and Protocol only — the project-direction gate), so these declared files
	/// carry the method name and the subscription without the interface. Every
	/// Application file that subscribes to the session end or declares
	/// <c>ResetSessionState</c> must appear here; an entry that stops being needed fails
	/// the exemption fact.
	/// </summary>
	private static readonly string[] ApplicationLayerExemptions =
	[
		"src/CasualtiesUnknownOnline.Application/Kernel/GuestCheckpointReceiver.cs",
		"src/CasualtiesUnknownOnline.Application/Kernel/IKernelBatchApplication.cs",
		"src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolService.cs"
	];

	private static readonly Regex SubscriptionRegex = new(
		@"[\w\.\(\)]+\.(?<event>SessionEnded|SessionActivated|MemberAdded|MemberRemoved|RemoteSceneChanged|EntryRepairRequested|LocalSceneReported)\s*(?<op>\+=|-=)\s*(?<handler>[^;]+?)\s*;",
		RegexOptions.Compiled);

	private static readonly Regex RetiredSpellingRegex = new(@"\bResetForSession(End)?\s*\(|\bResetSession\s*\(", RegexOptions.Compiled);

	private static readonly Regex ResetMethodRegex = new(@"\bvoid\s+ResetSessionState\s*\(", RegexOptions.Compiled);

	private static readonly Regex BindableHandlerRegex = new(@"^[A-Za-z_]\w*$", RegexOptions.Compiled);

	private static readonly Regex DeclarationRegex = new(@"\b(?:class|record|struct|interface)\s+\w+", RegexOptions.Compiled);

	private static readonly Regex CommentRegex = new(@"(?m)(^|\s)//[^\r\n]*|/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

	[Fact]
	public void EverySessionEndSubscriber_ReactsThroughTheOneContractMethod()
	{
		var failures = new List<string>();
		var runtimeCount = 0;
		var adapterCount = 0;
		foreach (var (path, text) in SourceFiles(ScannedRoots))
		{
			foreach (var subscription in Subscriptions(text).Where(s => s.Op == "+=" && s.Event == "SessionEnded"))
			{
				if (path.StartsWith(RuntimeRoot, StringComparison.Ordinal))
				{
					runtimeCount++;
				}
				else if (path.StartsWith(GameAdapterRoot, StringComparison.Ordinal))
				{
					adapterCount++;
				}
			}

			failures.AddRange(ContractFailures(path, text, ApplicationLayerExemptions));
		}

		if (runtimeCount < RuntimeSessionEndFloor)
		{
			failures.Add($"the scan found {runtimeCount} Runtime session-end subscriptions (floor {RuntimeSessionEndFloor})");
		}

		if (adapterCount != GameAdapterSessionEndCensus)
		{
			failures.Add($"the Game Adapter carries {adapterCount} session-end subscriptions (declared census {GameAdapterSessionEndCensus}) — its domains are torn down through its session binding's Bind/Unbind pairs, so a new subscription is a review decision");
		}

		Assert.True(
			File.ReadAllText(RepositoryPaths.File(ContractPath)).Contains("void " + ContractMethod + "();", StringComparison.Ordinal),
			$"{ContractPath} no longer declares the one session-teardown method this gate points at.");

		Assert.True(failures.Count == 0, "Session-teardown contract failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void EverySessionLifecycleSubscription_HasItsUnbindHalf()
	{
		var failures = new List<string>();
		var count = 0;
		foreach (var (path, text) in SourceFiles(ScannedRoots))
		{
			count += Subscriptions(text).Count;
			failures.AddRange(UnbindFailures(path, text));
		}

		if (count < FrameworkSubscriptionFloor)
		{
			failures.Add($"the scan found {count} session-lifecycle subscriptions (floor {FrameworkSubscriptionFloor})");
		}

		Assert.True(failures.Count == 0, "A session-lifecycle subscription is missing its unbind half" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheRetiredSessionResetSpellings_HaveNotComeBack()
	{
		var failures = new List<string>();
		var files = 0;
		foreach (var (path, text) in SourceFiles(["src"]))
		{
			files++;
			foreach (Match match in RetiredSpellingRegex.Matches(StripComments(text)))
			{
				failures.Add($"{path}: retired session-reset spelling '{match.Value.TrimEnd('(', ' ')}' — a session reset is {ContractMethod}");
			}
		}

		if (files < SourceFileFloor)
		{
			failures.Add($"the scan read {files} source files (floor {SourceFileFloor})");
		}

		Assert.True(failures.Count == 0, "A retired session-reset spelling came back" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void EverySessionResetMethod_LivesInATypeThatDeclaresTheContract()
	{
		var failures = new List<string>();
		var count = 0;
		foreach (var (path, text) in SourceFiles([RuntimeRoot.TrimEnd('/'), ApplicationRoot.TrimEnd('/')]))
		{
			var declarations = ResetDeclarations(text);
			count += declarations.Count;
			if (path.StartsWith(RuntimeRoot, StringComparison.Ordinal))
			{
				if (declarations.Count > 0 && !DeclaresContract(text))
				{
					failures.Add($"{path}: declares {ContractMethod} without declaring {ContractInterface}");
				}
			}
			else if (declarations.Count > 0 && !ApplicationLayerExemptions.Contains(path, StringComparer.Ordinal))
			{
				failures.Add($"{path}: an Application-layer {ContractMethod} declaration outside the declared exemption list");
			}
		}

		if (count < ResetMethodFloor)
		{
			failures.Add($"the scan found {count} {ContractMethod} declarations (floor {ResetMethodFloor})");
		}

		Assert.True(failures.Count == 0, "A session-reset method lives outside the contract" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheApplicationLayerExemption_IsStillNeededAndStillComplete()
	{
		var failures = new List<string>();
		foreach (var path in ApplicationLayerExemptions)
		{
			var file = RepositoryPaths.File(path);
			if (!File.Exists(file))
			{
				failures.Add($"{path} is listed as a layer exemption but does not exist");
				continue;
			}

			var text = File.ReadAllText(file);
			var subscribes = Subscriptions(text).Any(s => s.Op == "+=" && s.Event == "SessionEnded");
			if (!subscribes && ResetDeclarations(text).Count == 0)
			{
				failures.Add($"{path} is listed as a layer exemption but neither subscribes SessionEnded nor declares {ContractMethod}");
			}

			if (DeclaresContract(text))
			{
				failures.Add($"{path} now declares {ContractInterface} — remove it from the exemption list");
			}
		}

		foreach (var (path, text) in SourceFiles([ApplicationRoot.TrimEnd('/')]))
		{
			if (ApplicationLayerExemptions.Contains(path, StringComparer.Ordinal))
			{
				continue;
			}

			if (Subscriptions(text).Any(s => s.Op == "+=" && s.Event == "SessionEnded"))
			{
				failures.Add($"{path}: an Application-layer session-end subscriber outside the declared exemption list");
			}

			if (ResetDeclarations(text).Count > 0)
			{
				failures.Add($"{path}: an Application-layer {ContractMethod} declaration outside the declared exemption list");
			}
		}

		Assert.True(failures.Count == 0, "The Application-layer exemption drifted" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheScanner_FlagsANewSubscriberOutsideTheContract()
	{
		const string Sample = """
			public sealed class Widget
			{
				public Widget(ISessionControl session) => session.SessionEnded += OnSessionEnded;

				private void OnSessionEnded() => _channel.Clear();

				public void Dispose() => _session.SessionEnded -= OnSessionEnded;
			}
			""";
		var failures = ContractFailures(RuntimeRoot + "Session/Widget.cs", Sample, []);
		Assert.Contains(failures, failure => failure.Contains(ContractMethod, StringComparison.Ordinal));
		Assert.Contains(failures, failure => failure.Contains(ContractInterface, StringComparison.Ordinal));
		Assert.Empty(UnbindFailures(RuntimeRoot + "Session/Widget.cs", Sample));
	}

	[Fact]
	public void TheScanner_FlagsAMissedUnsubscribe()
	{
		const string Sample = """
			public sealed class Widget : ISessionReset
			{
				public Widget(ISessionControl session) => session.SessionEnded += ResetSessionState;

				public void ResetSessionState() => _channel.Clear();
			}
			""";
		Assert.Empty(ContractFailures(RuntimeRoot + "Session/Widget.cs", Sample, []));
		Assert.Contains(
			UnbindFailures(RuntimeRoot + "Session/Widget.cs", Sample),
			failure => failure.Contains("has no matching", StringComparison.Ordinal));
	}

	[Fact]
	public void TheScanner_FlagsAnUnbindableHandlerShape()
	{
		const string Lambda = """
			public sealed class Widget : ISessionReset
			{
				public Widget(ISessionControl session) => session.SessionEnded += () => ResetSessionState();

				public void ResetSessionState() => _channel.Clear();
			}
			""";
		const string Qualified = """
			public sealed class Widget : ISessionReset
			{
				public Widget(ISessionControl session) => session.SessionEnded += this.ResetSessionState;

				public void ResetSessionState() => _channel.Clear();
			}
			""";
		Assert.Contains(
			UnbindFailures(RuntimeRoot + "Session/Widget.cs", Lambda),
			failure => failure.Contains("must name a method", StringComparison.Ordinal));
		Assert.Contains(
			UnbindFailures(RuntimeRoot + "Session/Widget.cs", Qualified),
			failure => failure.Contains("must name a method", StringComparison.Ordinal));
	}

	[Fact]
	public void TheScanner_IgnoresAnUnbindThatOnlyExistsInAComment()
	{
		const string Sample = """
			public sealed class Widget : ISessionReset
			{
				public Widget(ISessionControl session) => session.SessionEnded += ResetSessionState;

				public void ResetSessionState() => _channel.Clear();

				// public void Dispose() => _session.SessionEnded -= ResetSessionState;
			}
			""";
		Assert.Contains(
			UnbindFailures(RuntimeRoot + "Session/Widget.cs", Sample),
			failure => failure.Contains("has no matching", StringComparison.Ordinal));
	}

	[Fact]
	public void TheScanner_AcceptsThePairedContractShape()
	{
		const string Sample = """
			public sealed class Widget : ISessionReset, IDisposable
			{
				public Widget(ISessionControl session) => session.SessionEnded += ResetSessionState;

				public void ResetSessionState() => _channel.Clear();

				public void Dispose() => _session.SessionEnded -= ResetSessionState;
			}
			""";
		Assert.Empty(ContractFailures(RuntimeRoot + "Session/Widget.cs", Sample, []));
		Assert.Empty(UnbindFailures(RuntimeRoot + "Session/Widget.cs", Sample));
	}

	[Fact]
	public void TheRetiredSpellingMatcher_FlagsTheRetiredNamesOnly()
	{
		Assert.NotEmpty(RetiredSpellingRegex.Matches("_session.SessionEnded += ResetForSessionEnd();"));
		Assert.NotEmpty(RetiredSpellingRegex.Matches("public void ResetForSession() { }"));
		Assert.NotEmpty(RetiredSpellingRegex.Matches("_playerStream.ResetSession();"));
		Assert.Empty(RetiredSpellingRegex.Matches("public void ResetSessionState() { }"));
		Assert.Empty(RetiredSpellingRegex.Matches("the retired spellings are ResetForSessionEnd and ResetSession, named in prose"));
	}

	private static IReadOnlyList<string> ContractFailures(string path, string text, IReadOnlyCollection<string> layerExempt)
	{
		var failures = new List<string>();
		var isRuntime = path.StartsWith(RuntimeRoot, StringComparison.Ordinal);
		var isApplication = path.StartsWith(ApplicationRoot, StringComparison.Ordinal);
		if (!isRuntime && !isApplication)
		{
			// The Game Adapter wires its domains through its session binding
			// (Bind/Unbind pairs); the naming contract is a Runtime contract, and the
			// adapter is covered by the pairing rule and the declared census.
			return failures;
		}

		foreach (var subscription in Subscriptions(text).Where(s => s.Op == "+=" && s.Event == "SessionEnded"))
		{
			if (isApplication && !layerExempt.Contains(path, StringComparer.Ordinal))
			{
				failures.Add($"{path}: an Application-layer session-end subscriber outside the declared exemption list");
			}

			if (subscription.Handler != ContractMethod)
			{
				failures.Add($"{path}: SessionEnded is handled by {subscription.Handler} — the one contract method is {ContractMethod}");
			}

			if (isRuntime && !DeclaresContract(text))
			{
				failures.Add($"{path}: subscribes to SessionEnded without declaring {ContractInterface}");
			}
		}

		return failures;
	}

	private static IReadOnlyList<string> UnbindFailures(string path, string text)
	{
		var failures = new List<string>();
		foreach (var subscription in Subscriptions(text))
		{
			if (!BindableHandlerRegex.IsMatch(subscription.Handler))
			{
				failures.Add($"{path}: {subscription.Event} {subscription.Op} {subscription.Handler} — a session-lifecycle subscription must name a method declared in this type (a lambda or a qualified alias can be neither asserted nor unbound)");
				continue;
			}

			if (subscription.Op == "+=" && !HasUnbind(text, subscription))
			{
				failures.Add($"{path}: {subscription.Event} += {subscription.Handler} has no matching {subscription.Event} -= in the same file");
			}
		}

		return failures;
	}

	private static bool HasUnbind(string text, (string Event, string Handler, string Op) subscription) =>
		Regex.IsMatch(StripComments(text), Regex.Escape("." + subscription.Event) + @"\s*-=\s*" + Regex.Escape(subscription.Handler) + @"\s*;");

	private static bool DeclaresContract(string text)
	{
		var stripped = StripComments(text);
		foreach (Match declaration in DeclarationRegex.Matches(stripped))
		{
			var brace = stripped.IndexOf('{', declaration.Index);
			var head = brace < 0 ? stripped[declaration.Index..] : stripped[declaration.Index..brace];
			if (head.Contains(ContractInterface, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static IReadOnlyList<string> ResetDeclarations(string text) =>
		[.. ResetMethodRegex.Matches(StripComments(text)).Select(match => match.Value.Trim())];

	private static IReadOnlyList<(string Event, string Handler, string Op)> Subscriptions(string text) =>
		[.. SubscriptionRegex.Matches(StripComments(text))
			.Where(match => match.Groups["handler"].Value.Trim() != "value")
			.Select(match => (match.Groups["event"].Value, match.Groups["handler"].Value.Trim(), match.Groups["op"].Value))];

	private static string StripComments(string text) => CommentRegex.Replace(text, "$1 ");

	private static IEnumerable<(string Path, string Text)> SourceFiles(IReadOnlyList<string> roots)
	{
		foreach (var root in roots)
		{
			foreach (var file in Directory.EnumerateFiles(RepositoryPaths.File(root), "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
			{
				if (file.Contains(@"\obj\", StringComparison.Ordinal) || file.Contains(@"\bin\", StringComparison.Ordinal))
				{
					continue;
				}

				yield return (Path.GetRelativePath(RepositoryPaths.Root, file).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllText(file));
			}
		}
	}
}
