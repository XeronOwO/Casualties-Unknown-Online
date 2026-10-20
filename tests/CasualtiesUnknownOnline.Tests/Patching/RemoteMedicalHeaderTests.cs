using System;
using System.IO;
using CasualtiesUnknownOnline.GameAdapter.Character;
using Xunit;
using static CasualtiesUnknownOnline.GameAdapter.Character.RemoteMedicalHeaderState;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// L0 regression surface for the remote medical panel's identity header. The bug: the header
/// (<c>bodyStatText</c>, <c>versionText</c>, <c>cInfo</c>) is written once per character by
/// <c>WoundView.SetCharDetails</c>, so a remote focus left the VIEWER's identity over the viewed
/// player's body. The adapter is Unity-bound, so the cases pin the Unity-free borrow rule
/// (<see cref="RemoteMedicalHeaderState"/>) and the wiring by source anchors — the same shape the
/// remote focus's local-controls tests use.
/// </summary>
public class RemoteMedicalHeaderTests
{
	/// <summary>The viewer's own rolled identity (the batch <c>20261023-b</c> live values).</summary>
	private static int[] Viewer => [135, 23, 44960, 6];

	/// <summary>The viewed player's own rolled identity from the same batch.</summary>
	private static int[] Viewed => [148, 18, 83756, 8];

	[Fact]
	public void Refresh_ShowsTheViewedPlayersValues_AndKeepsTheViewersOwnForTheSnapshot()
	{
		var state = new RemoteMedicalHeaderState();

		var write = state.Refresh([.. Viewer], [.. Viewed]);

		Assert.Equal(WriteKind.Show, write.Kind);
		Assert.Equal(Viewed, write.Values);

		// The panel now displays the viewed player, but this client's own 1 Hz snapshot must keep
		// reporting the VIEWER's identity — otherwise the viewed identity would spread to the peers
		// that view this client.
		Assert.Equal(Viewer, state.OwnIdentity([.. Viewed]));
	}

	[Fact]
	public void Refresh_SameValuesAgain_DoesNotRewriteThePanel()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);

		// The takeover wrote the viewed values into the panel, so the live panel now carries them.
		Assert.Equal(WriteKind.None, state.Refresh([.. Viewed], [.. Viewed]).Kind);
	}

	[Fact]
	public void Refresh_ANewIdentityOnTheSnapshot_FollowsIt()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);

		var changed = new[] { 150, 19, 111, 9 };
		var write = state.Refresh([.. Viewed], changed);

		Assert.Equal(WriteKind.Show, write.Kind);
		Assert.Equal(changed, write.Values);
		Assert.Equal(Viewer, state.OwnIdentity([.. changed]));
	}

	[Fact]
	public void Refresh_NoIdentity_BlanksOnceThenShowsItWhenItArrives()
	{
		var state = new RemoteMedicalHeaderState();

		Assert.Equal(WriteKind.Blank, state.Refresh([.. Viewer], null).Kind);
		Assert.Equal(WriteKind.None, state.Refresh([.. Viewer], null).Kind);
		Assert.Equal(WriteKind.Show, state.Refresh([.. Viewer], [.. Viewed]).Kind);
	}

	[Fact]
	public void Refresh_AnIdentityThatIsNotTheNativeFour_IsTreatedAsUnknown()
	{
		var state = new RemoteMedicalHeaderState();

		Assert.Equal(WriteKind.Blank, state.Refresh([.. Viewer], [148, 18, 83756]).Kind);
	}

	[Fact]
	public void TryAdoptOwn_WhileBorrowed_KeepsThePanelShowingTheViewedPlayer()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);

		var restored = new[] { 154, 12, 80646, 6 };
		Assert.True(state.TryAdoptOwn([.. Viewed], restored));

		// The restore did not flash the viewer's own identity onto the borrowed panel: the refresh
		// still has nothing to write, and the close gives the restored values back.
		Assert.Equal(WriteKind.None, state.Refresh([.. Viewed], [.. Viewed]).Kind);
		Assert.Equal(restored, state.OwnIdentity([.. Viewed]));
		Assert.Equal(restored, state.End([.. Viewed]));
	}

	[Fact]
	public void TryAdoptOwn_WithNoFocus_LeavesTheWriteToTheCaller()
	{
		var state = new RemoteMedicalHeaderState();

		Assert.False(state.TryAdoptOwn([.. Viewer], [.. Viewed]));
		Assert.Null(state.End([.. Viewer]));
	}

	[Fact]
	public void End_GivesTheViewersOwnValuesBackOnce()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);

		Assert.Equal(Viewer, state.End([.. Viewed]));
		Assert.Null(state.End([.. Viewed]));
	}

	[Fact]
	public void End_AfterABlank_StillGivesTheLabelsBack()
	{
		var state = new RemoteMedicalHeaderState();

		// The labels were blanked; cInfo was left describing the viewer's own character, so closing
		// writes that identity back and the labels become complete again.
		state.Refresh([.. Viewer], null);
		Assert.Equal(Viewer, state.End([.. Viewer]));
	}

	[Fact]
	public void Refresh_ShowThenBlank_KeepsTheCaptureGuardAndTheCloseRestore()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);

		// The takeover wrote the viewed values into cInfo; the blank writes the labels only.
		Assert.Equal(WriteKind.Blank, state.Refresh([.. Viewed], null).Kind);

		// The blank must not lose WHICH values the panel carries: this client's own snapshot keeps
		// reporting the viewer's identity, and the close gives the viewer's values back.
		Assert.Equal(Viewer, state.OwnIdentity([.. Viewed]));
		Assert.Equal(Viewer, state.End([.. Viewed]));
	}

	[Fact]
	public void Refresh_ShowThenBlankThenShowAgain_RestoresTheLabelsAndTheBorrow()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);
		state.Refresh([.. Viewed], null);

		// The labels are blank, so the same values arriving again must be written back (a blank is not
		// "already shows it").
		Assert.Equal(WriteKind.Show, state.Refresh([.. Viewed], [.. Viewed]).Kind);
		Assert.Equal(Viewer, state.OwnIdentity([.. Viewed]));
		Assert.Equal(Viewer, state.End([.. Viewed]));
	}

	[Fact]
	public void TryAdoptOwn_AfterABlank_StillKeepsTheBorrowedPanel()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);
		state.Refresh([.. Viewed], null);

		var restored = new[] { 154, 12, 80646, 6 };
		Assert.True(state.TryAdoptOwn([.. Viewed], restored));
		Assert.Equal(restored, state.End([.. Viewed]));
	}

	[Fact]
	public void End_AfterABlankAndARewrittenPanel_LeavesTheNewerValuesInPlace()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);
		state.Refresh([.. Viewed], null);

		var newPanel = new[] { 170, 40, 555, 2 };
		Assert.Null(state.End(newPanel));
		Assert.Equal(newPanel, state.OwnIdentity(newPanel));
	}

	[Fact]
	public void End_WithNoBorrow_WritesNothing() =>
		Assert.Null(new RemoteMedicalHeaderState().End([.. Viewer]));

	[Fact]
	public void End_AfterThePanelWasRewritten_LeavesTheNewerValuesInPlace()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);

		// A reloaded scene's own SetCharDetails wrote the next character: the borrow describes a
		// panel that no longer exists, and restoring over the newer write would put one character's
		// identity on another's panel.
		var newPanel = new[] { 170, 40, 555, 2 };
		Assert.Null(state.End(newPanel));
		Assert.Equal(newPanel, state.OwnIdentity(newPanel));
	}

	[Fact]
	public void OwnIdentity_AStaleBorrow_DoesNotAnswerForThePanel()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);

		var newPanel = new[] { 170, 40, 555, 2 };
		Assert.Equal(newPanel, state.OwnIdentity(newPanel));

		// The stale borrow was dropped, so the next focus borrows the panel as it is now.
		Assert.Equal(WriteKind.Show, state.Refresh(newPanel, [.. Viewed]).Kind);
		Assert.Equal(newPanel, state.OwnIdentity([.. Viewed]));
	}

	[Fact]
	public void ANextFocus_BorrowsThePanelAsItIsThen()
	{
		var state = new RemoteMedicalHeaderState();
		state.Refresh([.. Viewer], [.. Viewed]);
		state.End([.. Viewed]);

		var other = new[] { 149, 30, 80777, 1 };
		Assert.Equal(WriteKind.Show, state.Refresh([.. Viewer], other).Kind);
		Assert.Equal(Viewer, state.OwnIdentity(other));
	}

	[Fact]
	public void OwnIdentityForCapture_IsTheViewersOwnOnlyWhileThePanelIsBorrowed()
	{
		var state = new RemoteMedicalHeaderState();
		Assert.Null(state.OwnIdentityForCapture([.. Viewer]));

		state.Refresh([.. Viewer], [.. Viewed]);
		Assert.Null(state.OwnIdentityForCapture([.. Viewer])); // not the panel the borrow wrote
		Assert.Equal(Viewer, state.OwnIdentityForCapture([.. Viewed]));
	}

	[Fact]
	public void Reassert_PutsTheFocusDisplayBackAfterATemporaryCapture()
	{
		var state = new RemoteMedicalHeaderState();
		Assert.Equal(WriteKind.None, state.Reassert().Kind);

		state.Refresh([.. Viewer], [.. Viewed]);
		Assert.False(state.LabelsBlank);
		Assert.Equal(WriteKind.Show, state.Reassert().Kind);
		Assert.Equal(Viewed, state.Reassert().Values);

		// The capture replaced cInfo too, so after a blank the values are written back and the labels
		// are blanked again — the panel ends up exactly as the blank left it.
		state.Refresh([.. Viewed], null);
		Assert.True(state.LabelsBlank);
		Assert.Equal(WriteKind.Show, state.Reassert().Kind);
		Assert.Equal(Viewed, state.Reassert().Values);

		// A blank before the focus ever wrote anything needs only the labels.
		var fresh = new RemoteMedicalHeaderState();
		fresh.Refresh([.. Viewer], null);
		Assert.Equal(WriteKind.Blank, fresh.Reassert().Kind);
	}

	[Fact]
	public void TheNativeSave_SeesTheViewersOwnIdentityAndNotTheBorrowedHeader()
	{
		var patches = AdapterSource("Patches", "RemoteMedicalPatches.cs");

		// The game's save copies cInfo straight into save.sv (SaveSystem.cs:172-175): the one native
		// reader that assumes the header describes the LOCAL character.
		Assert.Contains("[HarmonyPatch(typeof(SaveSystem), \"SaveGame\")]", patches);
		Assert.Contains("__state = RemoteMedicalHeader.EnterOwnIdentityCapture();", patches);
		Assert.Contains("__state?.Dispose();", patches);

		// The window writes the viewer's own values through the native entry and re-asserts afterwards.
		var header = AdapterSource("Character", "RemoteMedicalHeader.cs");
		Assert.Contains("OwnIdentityForCapture(", header);
		Assert.Contains("State.Reassert()", header);
	}

	[Fact]
	public void TheClosePath_GivesTheHeaderBackBeforeItCanSkipTheRestore()
	{
		var close = AdapterSource("Character", "RemoteMedicalView.cs");
		var restore = close.IndexOf("RemoteMedicalHeader.Restore();", StringComparison.Ordinal);
		var earlyReturn = close.IndexOf("if (!wasOpen)", StringComparison.Ordinal);

		Assert.True(restore >= 0, "RemoteMedicalView.Close must give the identity header back");
		Assert.True(earlyReturn >= 0, "RemoteMedicalView.Close's early return moved — re-check the restore order");
		Assert.True(restore < earlyReturn, "the header restore must run before the early return can skip it");
	}

	[Fact]
	public void TheFocusFollowsTheSnapshotsIdentity_OnOpenAndOnEveryRefresh()
	{
		var coordinator = AdapterSource("", "RemoteMedicalCoordinator.cs");

		Assert.Equal(2, Count(coordinator, "RemoteMedicalHeader.ShowViewedIdentity("));

		// The focus's first rendered frame already shows the viewed player: the takeover sits after
		// the native panel was opened.
		var open = coordinator.IndexOf("PlayerCamera.main.WoundViewButton();", StringComparison.Ordinal);
		var takeover = coordinator.IndexOf("RemoteMedicalHeader.ShowViewedIdentity(", StringComparison.Ordinal);
		Assert.True(open >= 0 && takeover > open, "the header takeover must follow the native panel open");
		Assert.Contains("data.NativeFields?.CharacterInfo", coordinator);
	}

	[Fact]
	public void TheLocalIdentityPort_UsesTheBorrowedPanelsRememberedValues()
	{
		var fields = AdapterSource("Character", "CharacterNativeFields.cs");

		// The local snapshot's identity comes from the focus while the panel is borrowed...
		Assert.Contains("RemoteMedicalHeader.OwnIdentity(woundView.cInfo)", fields);

		// ...and the local restore's own write is adopted instead of overwriting the borrowed panel.
		Assert.Contains("RemoteMedicalHeader.TryAdoptOwn(woundView.cInfo, details)", fields);
	}

	private static int Count(string text, string token)
	{
		var count = 0;
		for (var index = text.IndexOf(token, StringComparison.Ordinal);
			index >= 0;
			index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	private static string AdapterSource(string folder, string fileName) =>
		File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"src",
			"CasualtiesUnknownOnline.GameAdapter",
			folder,
			fileName));

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException("could not locate repository root (CasualtiesUnknownOnline.slnx)");
		}

		return directory.FullName;
	}
}
