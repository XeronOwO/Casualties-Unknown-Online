using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The mod half of the structured handshake refusal (ticket
/// `handshake-identity-and-refusal-report.md`): the existing NetworkMode contract,
/// returned as a value so a refusal can name the mod and the reason. The
/// judgments are unchanged — a client-only difference never refuses, a
/// state-bearing one always answers.
/// </summary>
[Trait("Category", "Unit")]
public class HandshakeModAdmissionTests
{
	private static ModManifest Host(string id, string version, NetworkMode mode, ModPermission permissions = ModPermission.None, string? binding = null) =>
		new(id, id, version, mode, description: null, permissions: permissions, nativeBinding: binding);

	private static ModInfoMsg Guest(string id, string version, NetworkMode mode, ModPermission permissions = ModPermission.None, string? binding = null) =>
		new() { Id = id, Version = version, NetworkMode = mode, Permissions = permissions, NativeBinding = binding };

	private static ModAdmissionResult Evaluate(
		IReadOnlyList<ModManifest> host,
		IReadOnlyList<ModInfoMsg> guest,
		NativeBindingParity parity = NativeBindingParity.Allow,
		IReadOnlyDictionary<string, string>? content = null) =>
		HandshakeModAdmission.Evaluate(host, guest, content ?? new Dictionary<string, string>(), parity);

	[Fact]
	public void TheSameStateBearingMod_IsAdmitted()
	{
		var result = Evaluate([Host("a", "1.0.0", NetworkMode.Synchronized)], [Guest("a", "1.0.0", NetworkMode.Synchronized)]);

		Assert.Null(result.Refusal);
	}

	[Fact]
	public void AMissingStateBearingMod_RefusesByModSetNamingIt()
	{
		var result = Evaluate([Host("a", "1.0.0", NetworkMode.Synchronized)], []);

		Assert.NotNull(result.Refusal);
		Assert.Equal(HandshakeDimension.ModSet, result.Refusal!.Dimension);
		Assert.Equal("a", result.Refusal.Detail);
	}

	[Fact]
	public void AVersionMismatchOnAStateBearingMod_RefusesByModVersionNamingIt()
	{
		var result = Evaluate([Host("a", "1.0.0", NetworkMode.Synchronized)], [Guest("a", "2.0.0", NetworkMode.Synchronized)]);

		Assert.NotNull(result.Refusal);
		Assert.Equal(HandshakeDimension.ModVersion, result.Refusal!.Dimension);
		Assert.Equal("a", result.Refusal.Detail);
	}

	[Fact]
	public void AClientOnlyModMissingOnTheGuest_IsAdmitted()
	{
		var result = Evaluate([Host("cosmetic", "1.0.0", NetworkMode.ClientOnly)], []);

		Assert.Null(result.Refusal);
	}

	[Fact]
	public void AHostOnlyModMissingOnTheGuest_IsAdmitted()
	{
		var result = Evaluate([Host("host-side", "1.0.0", NetworkMode.HostOnly)], []);

		Assert.Null(result.Refusal);
	}

	[Fact]
	public void TheGuestListingAClientOnlyModTheHostDoesNotRun_IsAdmitted()
	{
		var result = Evaluate([], [Guest("local", "1.0.0", NetworkMode.ClientOnly)]);

		Assert.Null(result.Refusal);
	}

	[Fact]
	public void TheGuestClaimingAStateBearingModTheHostLacks_RefusesByModSet()
	{
		var result = Evaluate([], [Guest("claims", "1.0.0", NetworkMode.Synchronized)]);

		Assert.NotNull(result.Refusal);
		Assert.Equal(HandshakeDimension.ModSet, result.Refusal!.Dimension);
		Assert.Equal("claims", result.Refusal.Detail);
	}

	[Fact]
	public void ADuplicateIdInTheGuestList_RefusesByModSet()
	{
		var result = Evaluate([], [Guest("dup", "1.0.0", NetworkMode.ClientOnly), Guest("dup", "1.0.0", NetworkMode.ClientOnly)]);

		Assert.NotNull(result.Refusal);
		Assert.Equal(HandshakeDimension.ModSet, result.Refusal!.Dimension);
	}

	[Fact]
	public void APermissionMismatchOnAStateBearingMod_RefusesByModSet()
	{
		var result = Evaluate(
			[Host("a", "1.0.0", NetworkMode.Synchronized, ModPermission.ReadGameState)],
			[Guest("a", "1.0.0", NetworkMode.Synchronized, ModPermission.SendNetworkMessage)]);

		Assert.NotNull(result.Refusal);
		Assert.Equal(HandshakeDimension.ModSet, result.Refusal!.Dimension);
	}

	[Fact]
	public void ABindingDifferenceUnderRequire_RefusesByModSet()
	{
		var result = Evaluate(
			[Host("a", "1.0.0", NetworkMode.Synchronized, binding: "Assembly-CSharp:PlayerCamera")],
			[Guest("a", "1.0.0", NetworkMode.Synchronized, binding: "Assembly-CSharp:Item")],
			NativeBindingParity.Require);

		Assert.NotNull(result.Refusal);
		Assert.Equal(HandshakeDimension.ModSet, result.Refusal!.Dimension);
	}

	[Fact]
	public void ABindingDifferenceUnderWarn_IsAdmittedWithANote()
	{
		var result = Evaluate(
			[Host("a", "1.0.0", NetworkMode.Synchronized, binding: "Assembly-CSharp:PlayerCamera")],
			[Guest("a", "1.0.0", NetworkMode.Synchronized, binding: "Assembly-CSharp:Item")],
			NativeBindingParity.Warn);

		Assert.Null(result.Refusal);
		Assert.Contains(result.Notes, note => note.Kind == ModAdmissionNoteKind.BindingWarn && note.ModId == "a");
	}

	[Fact]
	public void AContentDifference_IsReportedAndNeverRefused()
	{
		var result = Evaluate(
			[Host("a", "1.0.0", NetworkMode.Synchronized)],
			[new ModInfoMsg { Id = "a", Version = "1.0.0", NetworkMode = NetworkMode.Synchronized, ContentFingerprint = "member" }],
			content: new Dictionary<string, string> { ["a"] = "host" });

		Assert.Null(result.Refusal);
		Assert.Contains(result.Notes, note => note.Kind == ModAdmissionNoteKind.ContentParity && note.ModId == "a");
	}
}
