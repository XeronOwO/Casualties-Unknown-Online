using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Gate for the Mod API governance rule's version discipline: a LIVE governance
/// document states the rule and points at the constant, it never restates the
/// number. The number lives in exactly one place — <c>ProtocolVersion.Current</c>
/// — and its own doc comment is the wire-change log, so a copied number is a
/// future lie (the drift this gate was written for: the mod API contract claimed
/// 31 while the constant was 34, and the decision that defines the numbering
/// policy claimed 31 too).
///
/// Scan surface and its boundary, stated rather than implied: only the documents
/// that state CURRENT facts are scanned — <c>AGENTS.md</c>, the live architecture,
/// development and evidence pages named in <see cref="LiveDocumentPaths"/>, the
/// active decision register, and the six mod-facing reference pages under
/// <c>docs/en|zh/reference/</c> (the mod API contract, the modification policy and
/// the protocol-message table, in both blocks) that state the contract a mod codes
/// against.
/// Records of a past state (<c>docs/evidence/**</c> self-checks, backlog tickets,
/// the architecture evolution logs) are exempt on purpose: they are history, they
/// must keep the number they were written with, and rewriting them would destroy
/// the record. What the gate recognizes is the CLAIM shape (a claim word or
/// <c>=</c> followed by a number), so a historical arrow such as "23 → 24" and a
/// bare pointer to the constant both pass; a phrasing that claims the current value
/// without one of those words is the matcher's known blind spot.
/// </summary>
public class ProtocolNumberGateTests
{
	private const string VersionSource = "src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs";

	/// <summary>Census floor: the live surface carries fifteen documents (measured 2026-09-25); the fixed paths are existence-checked, so a scan that finds fewer is a broken scan or a deleted live doc, not a clean tree.</summary>
	private const int LiveDocumentFloor = 15;

	private static readonly string[] LiveDocumentPaths =
	[
		"AGENTS.md",
		"docs/README.md",
		"docs/decisions/active.md",
		"docs/architecture/README.md",
		"docs/architecture/current.md",
		"docs/architecture/protocol.md",
		"docs/architecture/domains.md",
		"docs/development/agent-reference.md",
		"docs/evidence/normative-gates.md",
		"docs/en/reference/mod-api.md",
		"docs/en/reference/modification-policy.md",
		"docs/en/reference/protocol-messages.md",
		"docs/zh/reference/mod-api.md",
		"docs/zh/reference/modification-policy.md",
		"docs/zh/reference/protocol-messages.md"
	];

	private static readonly Regex VersionLineRegex = new(@"ProtocolVersion\.Current|protocol version", RegexOptions.IgnoreCase);
	private static readonly Regex AssignmentRegex = new(@"ProtocolVersion\.Current\s*=\s*`?(\d{1,3})");
	private static readonly Regex ClaimRegex = new(@"(?i)(?:\b(?:is|are|was|were|now|currently|stays?|stayed|becomes?|became|equals?|bumps?|bumped(?:\s+to)?|to)\b|(?<![#\w])[=:])\s*`?(\d{1,3})`?");

	[Fact]
	public void LiveGovernanceDocuments_DoNotRestateTheProtocolVersionNumber()
	{
		var failures = new List<string>();
		var documents = LiveDocuments().ToList();
		foreach (var document in LiveDocumentPaths)
		{
			if (!File.Exists(RepositoryPaths.File(document)))
			{
				failures.Add($"{document} is named as a live governance document but does not exist");
			}
		}

		foreach (var document in documents)
		{
			failures.AddRange(FindRestatedVersions(File.ReadAllText(RepositoryPaths.File(document))).Select(claim => $"{document}: {claim}"));
		}

		if (documents.Count < LiveDocumentFloor)
		{
			failures.Add($"the scan read {documents.Count} live documents (floor {LiveDocumentFloor})");
		}

		Assert.True(
			File.ReadAllText(RepositoryPaths.File(VersionSource)).Contains("public const int Current", StringComparison.Ordinal),
			$"{VersionSource} no longer declares the protocol version constant this gate points at.");

		Assert.True(
			failures.Count == 0,
			"A live governance document restates the protocol version number; point at `ProtocolVersion.Current` instead "
			+ "(its doc comment is the wire-change log)." + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheMatcher_FlagsACurrentValueClaimAndIgnoresThePointerForm()
	{
		Assert.NotEmpty(FindRestatedVersions("- `ProtocolVersion.Current` is `31`. The pre-release sequence was reset."));
		Assert.NotEmpty(FindRestatedVersions("The protocol version is 34 today."));
		Assert.NotEmpty(FindRestatedVersions("bump `ProtocolVersion.Current` to 35 in the same change"));
		Assert.NotEmpty(FindRestatedVersions("`ProtocolVersion.Current` = 31"));

		Assert.Empty(FindRestatedVersions("change the wire the mechanism needs and bump `ProtocolVersion.Current` in the same change (decision 137 is the numbering policy)."));
		Assert.Empty(FindRestatedVersions("the value lives in `ProtocolVersion.Current`; its doc comment lists the wire changes."));
		Assert.Empty(FindRestatedVersions("the protocol version sequence was deliberately reset before the first release."));
		Assert.Empty(FindRestatedVersions("the generation stamp landed with ProtocolVersion.Current 23 → 24."));
	}

	private static IEnumerable<string> LiveDocuments()
	{
		foreach (var document in LiveDocumentPaths)
		{
			yield return document;
		}
	}

	private static IReadOnlyList<string> FindRestatedVersions(string text)
	{
		var claims = new List<string>();
		foreach (var line in text.Split('\n'))
		{
			if (!VersionLineRegex.IsMatch(line))
			{
				continue;
			}

			var assignment = AssignmentRegex.Match(line);
			if (assignment.Success)
			{
				claims.Add($"states the version as {assignment.Groups[1].Value}");
				continue;
			}

			var claim = ClaimRegex.Match(line);
			if (claim.Success)
			{
				claims.Add($"claims the version is {claim.Groups[1].Value}");
			}
		}

		return claims;
	}
}
