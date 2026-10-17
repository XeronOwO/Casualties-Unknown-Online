using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The member-sender ↔ authority-declaration gate: the wire command kinds a sender
/// under <c>src/</c> actually emits and the kinds
/// <c>WireCommandAuthorityPolicy</c> declares a member may author are ONE list.
///
/// <para>
/// Why this is a gate rather than a unit test: the two facts live in two places
/// nothing ties together — a table in the Application layer and the framed
/// <c>SendCommand(new WireCommand { … })</c> call sites scattered from the item
/// flow to the enemy-combat submitter. The table's whole justification is that it
/// is filled from the senders this tree HAS, and no runtime test can enumerate
/// them (the guest side of a send is a role branch, and the test host composes
/// whichever nodes it likes), so the census is read from SOURCE. Either drift is a
/// real defect: a sender for a kind declared <c>HostOnly</c> is refused at runtime
/// on a path that used to work, and a kind declared member-authorable that no
/// sender emits is a permission the seam grants to a report nothing in this tree
/// can make.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the declared side reads the switch arms of
/// the policy's own file (<c>WireCommandKind.X =&gt; AuthorityKind.Y</c>); the
/// sender side reads the argument list of every <c>SendCommand(…)</c> call under
/// <c>src/</c> and takes the <c>Kind = WireCommandKind.X</c> initializer inside it,
/// with comments flattened first so a commented-out sender neither satisfies nor
/// breaks the census. Out of reach, and named rather than implied: a sender that
/// spells its kind through a local variable or a payload-type lookup, the
/// host-authored <c>SendCommandRejected</c> (an answer, not a member's report), and
/// every <c>SendCommand</c> call a test project makes — the census is <c>src/</c>
/// only. The matcher is pinned with one positive and two negative samples below.
/// </para>
/// </summary>
public class WireCommandSenderAuthorityGateTests
{
	/// <summary>The one file that declares what a member's submission carries.</summary>
	internal const string PolicyPath = "src/CasualtiesUnknownOnline.Application/Kernel/WireCommandAuthorityPolicy.cs";

	/// <summary>The tree the sender census reads, derived from the repository layout rather than listed.</summary>
	internal const string SourceRoot = "src";

	/// <summary>Census floors: the counts measured at 2026-10-14 — thirteen member-authored kinds and thirteen host-only kinds (the console write added the twelfth and the native-use report the thirteenth). A scan emptied alongside its source must fail here rather than pass by finding nothing.</summary>
	internal const int MemberAuthoredFloor = 13;

	internal const int HostOnlyFloor = 13;

	/// <summary>One policy arm, both halves captured: the kind and the authority it declares.</summary>
	private static readonly Regex DeclaredArm = new(@"WireCommandKind\.(\w+)\s*=>\s*AuthorityKind\.(\w+)", RegexOptions.Compiled);

	/// <summary>The initializer a framed send names its kind with.</summary>
	private static readonly Regex SentKind = new(@"Kind\s*=\s*WireCommandKind\.(\w+)", RegexOptions.Compiled);

	[Fact]
	public void EveryWireKindAMemberSenderEmits_IsDeclaredMemberAuthorable()
	{
		var declared = DeclaredKinds();
		var hostOnly = declared.Where(entry => entry.Value == "HostOnly").Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
		var memberAuthored = declared.Where(entry => entry.Value != "HostOnly").Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
		Assert.True(memberAuthored.Count >= MemberAuthoredFloor,
			$"the policy declares only {memberAuthored.Count} member-authored kind(s); the census expects at least {MemberAuthoredFloor}");
		Assert.True(hostOnly.Count >= HostOnlyFloor,
			$"the policy declares only {hostOnly.Count} host-only kind(s); the census expects at least {HostOnlyFloor}");

		var sent = MemberSentKinds();
		Assert.True(sent.Count >= MemberAuthoredFloor,
			$"the sender census found only {sent.Count} kind(s) under {SourceRoot}/; the scan is reading the wrong tree or the senders moved");

		var refusedAtRuntime = sent.Where(hostOnly.Contains).OrderBy(kind => kind, StringComparer.Ordinal).ToList();
		Assert.True(refusedAtRuntime.Count == 0,
			"a member sender emits a wire kind the admission seam refuses as host-only; that path is dropped at runtime."
			+ Environment.NewLine
			+ string.Join(Environment.NewLine, refusedAtRuntime.Select(kind => $"{kind}: sent under {SourceRoot}/, declared HostOnly in {PolicyPath}")));

		var grantedWithNoSender = memberAuthored.Where(kind => !sent.Contains(kind)).OrderBy(kind => kind, StringComparer.Ordinal).ToList();
		Assert.True(grantedWithNoSender.Count == 0,
			"the seam admits a wire kind no member sender in this tree emits; the declaration grants a permission nothing can use, so the kind belongs under HostOnly until a sender exists."
			+ Environment.NewLine
			+ string.Join(Environment.NewLine, grantedWithNoSender.Select(kind => $"{kind}: declared member-authored in {PolicyPath}, no send under {SourceRoot}/")));
	}

	[Fact]
	public void TheSenderScan_ReadsTheFramedCommandAndNothingElse()
	{
		// A kind named inside the send's own wire command is what the census counts.
		Assert.Equal(
			["ItemSpawn"],
			ScanSenders("_kernelProtocol.SendCommand(new WireCommand\n{\n\tKind = WireCommandKind.ItemSpawn,\n\tIdentity = id,\n}, WirePayloadType.ItemSpawnCommand);"));

		// The same kind in a mapper arm is not a send.
		Assert.Empty(ScanSenders("WireCommandKind.ItemSpawn => ItemSpawnWireMapper.FromWireCommand(command, operation, actor, epoch, authority, identity),"));

		// The host's own answer is not a member's report, and its name is not a call the scan reads.
		Assert.Empty(ScanSenders("_commandHandler.SendCommandRejected(targetSteamId, itemId, reason);"));
	}

	/// <summary>The policy's arms as a kind → authority-name map, read from its own source.</summary>
	private static Dictionary<string, string> DeclaredKinds()
	{
		var text = SourceScan.Flatten(File.ReadAllText(RepositoryPaths.File(PolicyPath)));
		var declared = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (Match match in DeclaredArm.Matches(text))
		{
			declared[match.Groups[1].Value] = match.Groups[2].Value;
		}

		return declared;
	}

	/// <summary>Every kind a framed <c>SendCommand</c> call site under the source root names, deduplicated.</summary>
	private static HashSet<string> MemberSentKinds()
	{
		var sent = new HashSet<string>(StringComparer.Ordinal);
		foreach (var path in SourceScan.SourceFiles(RepositoryPaths.File(SourceRoot)))
		{
			foreach (var kind in ScanSenders(File.ReadAllText(path)))
			{
				sent.Add(kind);
			}
		}

		return sent;
	}

	/// <summary>One file's send call sites: the argument list of every <c>SendCommand(…)</c> call, scanned for the kind its wire command names.</summary>
	private static List<string> ScanSenders(string source)
	{
		var text = SourceScan.Flatten(source);
		var kinds = new List<string>();
		foreach (var arguments in SourceScan.MemberCallArguments(text, "SendCommand"))
		{
			foreach (Match match in SentKind.Matches(arguments))
			{
				kinds.Add(match.Groups[1].Value);
			}
		}

		return kinds;
	}
}
