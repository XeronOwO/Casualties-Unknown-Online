using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The command console's save surface: <c>/save</c> ARMS a cut — it never takes
/// one inside the console callback — and the three answers the save layer produces
/// are printed where the player is already reading: the cut report the seam
/// resolved, the Continue click's own account (one notification plus the itemized
/// history), and a restore's live-world write. A cut nobody asked for (a layer
/// advance) stays in the log.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleSaveTests
{
	[Fact]
	public void Save_OnHost_ArmsACutForTheSeam()
	{
		var saves = new FakeWorldSaveControl { CanArm = true };
		var (host, _) = Session(saves);
		var console = FileConsole(host);

		Assert.True(console.TryExecute("/save"));

		Assert.Equal([WorldCutReason.Command], saves.RequestedReasons);
		Assert.Contains(console.Lines, line => line.Text.Contains("Save queued", StringComparison.Ordinal));
	}

	[Fact]
	public void Save_WhenTheHostCannotWrite_NamesTheReason()
	{
		var saves = new FakeWorldSaveControl { CanArm = false, Refusal = "this build has no CUO world repository" };
		var (host, _) = Session(saves);
		var console = FileConsole(host);

		Assert.True(console.TryExecute("/save"));

		Assert.Empty(saves.RequestedReasons);
		Assert.Contains(console.Lines, line => line.Text.Contains("Cannot save: this build has no CUO world repository.", StringComparison.Ordinal));
	}

	[Fact]
	public void Save_IsNotGatedByTheConsole_SoSoloReachesTheSaveLayer()
	{
		// The command is Anyone on purpose: solo play has no session role, so a
		// host-only console gate would make the one player-facing save trigger
		// unreachable there. The save layer owns the authority rule and answers a
		// guest with its own refusal (covered by the save-layer suites).
		var saves = new FakeWorldSaveControl { CanArm = true };
		var (_, guest) = Session(saves);
		var console = FileConsole(guest);

		Assert.True(console.TryExecute("/save"));

		Assert.Equal([WorldCutReason.Command], saves.RequestedReasons);
		Assert.DoesNotContain(console.Lines, line => line.Text.Contains("host-only", StringComparison.Ordinal));
	}

	[Fact]
	public void CutReport_IsPrintedForTheCutsThePlayerAskedFor()
	{
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);

		saves.Raise(new WorldCutReport(WorldCutResult.Captured, WorldCutReason.Command, "w-1", "world w-1 at revision 3", []));

		Assert.Contains(console.Lines, line => line.Text.Contains("cut written (world w-1 at revision 3)", StringComparison.Ordinal));
	}

	[Fact]
	public void CutReport_OfASystemTrigger_StaysOutOfTheConsole()
	{
		// A layer advance is not a player action: it belongs in the log, not in the
		// console the player reads for their own commands.
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);
		var before = console.Lines.Count;

		saves.Raise(new WorldCutReport(WorldCutResult.Captured, WorldCutReason.LayerAdvance, "w-1", "world w-1 at revision 3", []));

		Assert.Equal(before, console.Lines.Count);
	}

	[Fact]
	public void IncompleteRestore_IsPrintedAtErrorLevel()
	{
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);
		var audit = host.Services.GetRequiredService<WorldRestoreAudit>();
		audit.BeginRestore("w-9", restoreSequence: 1);

		audit.LiveWriteFinished(
			restoreSequence: 1,
			complete: false,
			refused: ["1 partial-damage row(s)"],
			summary: "the live world did not take 1 partial-damage row(s)");

		Assert.Contains(console.Lines, line => line.Text.Contains("did not take 1 partial-damage row(s)", StringComparison.Ordinal));
	}

	[Fact]
	public void RestoreReport_OfADamagedRestore_IsOneNotificationWithTheItemsInTheHistory()
	{
		// §6 forbids silent loss, and the console is the only in-game surface the save
		// system has. A damaged restore is ONE event: the disposition is the line the
		// player is interrupted by, and the itemized account (content ids included) goes
		// into the history — announced details would flood the closed console's
		// newest-few window and push every other notice out of it.
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);

		saves.RaiseRestore(new WorldRestoreReport(
			"w-3",
			WorldRestoreReport.Disposition.Applied,
			"world w-3 restored with damage: 1 untranslatable entry(ies) skipped in items.json (ContentMissing)",
			["entry bandage was skipped in items.json (ContentMissing): the item definition is gone"]));

		Assert.Contains(console.Lines, line => line.Text.Contains("CUO restored world w-3 with 1 damaged item(s)", StringComparison.Ordinal));
		Assert.Equal(2, console.Lines.Count(line => !line.Notifiable));
		Assert.Contains(console.Lines, line => line.Text.Contains("entry bandage was skipped", StringComparison.Ordinal) && !line.Notifiable);
	}

	[Fact]
	public void RestoreReport_OfACleanRestore_IsOneLineAndSaysNothingWasLost()
	{
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);
		var before = console.Lines.Count;

		saves.RaiseRestore(new WorldRestoreReport("w-4", WorldRestoreReport.Disposition.Applied, "world w-4 restored from the live snapshot", []));

		Assert.Contains(console.Lines, line => line.Text.Contains("CUO restored world w-4: nothing was lost.", StringComparison.Ordinal));
		// The account's own one line still reaches the history; nothing is announced twice.
		Assert.Contains(console.Lines, line => line.Text.Contains("restored from the live snapshot", StringComparison.Ordinal) && !line.Notifiable);
		Assert.Equal(before + 2, console.Lines.Count);
	}

	[Fact]
	public void StartingSupplies_OfAGrantedNewPlayer_IsOneNotificationNamingTheItems()
	{
		// S4.3's account on the same surface (decision 179): a player the world had no
		// character for is told what they were handed. ONE line — it names its items in that
		// same line, so there is nothing behind it to read and nothing else is pushed out of
		// the closed console's window.
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);
		var before = console.Lines.Count;

		host.Services.GetRequiredService<IStartingSupplyPublisher>().Publish(new StartingSupplyGrantReport(
			StartingSupplyGrantReport.Disposition.Granted,
			"full",
			["lantern", "dogfood", "waterbottle", "trashbag"],
			[]));

		var line = Assert.Single(console.Lines.Skip(before));
		Assert.True(line.Notifiable);
		Assert.Equal(ConsoleLineKind.Success, line.Kind);
		Assert.Contains("starting supplies (full) given", line.Text, StringComparison.Ordinal);
		Assert.Contains("lantern, dogfood, waterbottle, trashbag", line.Text, StringComparison.Ordinal);
	}

	[Fact]
	public void StartingSupplies_OfAPartialGrant_IsAnnouncedAsAnErrorAndNamesWhatStayedOnTheGround()
	{
		// The one failure shape: the game refused a slot (Body.PickUpItem refuses silently),
		// so the item is at the body's feet. A player who cannot find something they were
		// told they received must not have to read the line twice to learn it never landed.
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);

		host.Services.GetRequiredService<IStartingSupplyPublisher>().Publish(new StartingSupplyGrantReport(
			StartingSupplyGrantReport.Disposition.Granted,
			"full",
			["lantern", "waterbottle", "trashbag"],
			["dogfood"]));

		Assert.Contains(
			console.Lines,
			line => line.Kind == ConsoleLineKind.Error && line.Text.Contains("could not be placed", StringComparison.Ordinal) && line.Text.Contains("dogfood", StringComparison.Ordinal));
	}

	[Fact]
	public void StartingSupplies_OfAPlayerTheGameAlreadySupplied_SaysSoInsteadOfGranting()
	{
		// A fresh run's first layer, and a restored run frozen on its own starting layer: the
		// game's own grant runs inside generation, so CUO handed out nothing. The player still
		// gets the line — "the world already supplied you" is an answer, and without it the
		// mechanism reads as if it had never run.
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);

		host.Services.GetRequiredService<IStartingSupplyPublisher>().Publish(new StartingSupplyGrantReport(
			StartingSupplyGrantReport.Disposition.AlreadyOwned, "full", [], []));

		var line = Assert.Single(console.Lines, entry => entry.Text.Contains("already yours", StringComparison.Ordinal));
		Assert.True(line.Notifiable);
		Assert.Equal(ConsoleLineKind.Info, line.Kind);
	}

	[Fact]
	public void StartingSupplies_OfARunWithNoSupplies_SaysTheRunDecidedIt()
	{
		// "This run hands out nothing" is an answer, not an absence: a player who expects
		// supplies must be able to see that the run — not a broken mod — decided against them.
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);

		host.Services.GetRequiredService<IStartingSupplyPublisher>().Publish(new StartingSupplyGrantReport(
			StartingSupplyGrantReport.Disposition.Disabled, "none", [], []));

		var line = Assert.Single(console.Lines, entry => entry.Text.Contains("this run grants no starting supplies", StringComparison.Ordinal));
		Assert.True(line.Notifiable);
		Assert.Equal(ConsoleLineKind.Info, line.Kind);
	}

	[Fact]
	public void StartingSupplies_OfAGrantWhereNothingLanded_IsAnnouncedAsAnError()
	{
		// The worst shape: every slot refused, so the player is EMPTY-handed while the line
		// would otherwise open with "given —" and contradict itself two clauses later. The
		// report's own words are asserted for that case; here the console pins the level.
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);

		host.Services.GetRequiredService<IStartingSupplyPublisher>().Publish(new StartingSupplyGrantReport(
			StartingSupplyGrantReport.Disposition.Granted, "light", [], ["emergencylight"]));

		var line = Assert.Single(console.Lines, entry => entry.Text.Contains("could not be placed", StringComparison.Ordinal));
		Assert.Equal(ConsoleLineKind.Error, line.Kind);
		Assert.DoesNotContain("given", line.Text, StringComparison.Ordinal);
	}

	[Fact]
	public void RestoreReport_OfARefusal_NamesTheReasonTheClickDidNothing()
	{
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);

		saves.RaiseRestore(new WorldRestoreReport("", WorldRestoreReport.Disposition.Refused, "no CUO world exists to continue", []));

		Assert.Contains(console.Lines, line => line.Text.Contains("CUO continue refused: no CUO world exists to continue", StringComparison.Ordinal));
		Assert.Contains(console.Lines, line => line.Kind == ConsoleLineKind.Error);
	}

	[Fact]
	public void RestoreReport_OfARefusalWithDamage_KeepsTheReasonInTheLineAndTheItemsInTheHistory()
	{
		// The refusal is the case §6 exists for, and the one that carries the most detail:
		// a decode-level refusal's summary is a GROUPED count, so the content ids are only
		// in the itemized lines. The reason is the notification (it is what the player has
		// to act on) and every item follows it in the history — never the other way round.
		var saves = new FakeWorldSaveControl();
		var (host, _) = Session(saves);
		var console = FileConsole(host);

		saves.RaiseRestore(new WorldRestoreReport(
			"w-7",
			WorldRestoreReport.Disposition.Refused,
			"the snapshot has no readable run baseline (run.json); 1 untranslatable entry(ies) skipped in items.json (ContentMissing)",
			["entry bandage was skipped in items.json (ContentMissing): the item definition is gone"]));

		Assert.Contains(console.Lines, line => line.Notifiable && line.Text.Contains("CUO continue refused: the snapshot has no readable run baseline", StringComparison.Ordinal));
		Assert.Contains(console.Lines, line => !line.Notifiable && line.Text.Contains("entry bandage was skipped", StringComparison.Ordinal));
	}

	[Fact]
	public void Help_ListsTheSaveAndAdminCommands()
	{
		var (host, _) = Session(new FakeWorldSaveControl());

		Assert.True(FileConsole(host).TryExecute("/help"));

		var help = FileConsole(host).Lines;
		Assert.Contains(help, line => line.Text.Contains("/save", StringComparison.Ordinal));
		Assert.Contains(help, line => line.Text.Contains("/kick", StringComparison.Ordinal));
	}

	// ---- fixture ----

	private static (TestNode Host, TestNode Guest) Session(FakeWorldSaveControl saves)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: services => services.Replace(ServiceDescriptor.Singleton<IWorldSaveControl>(saves)));
		MarkInWorld(host);
		MarkInWorld(guest);
		return (host, guest);
	}

	private static ICommandControl FileConsole(TestNode node) => node.Services.GetRequiredService<ICommandControl>();

	/// <summary>A save control the console tests drive directly: it records what the console armed and lets a test raise the seam's answer.</summary>
	internal sealed class FakeWorldSaveControl : IWorldSaveControl
	{
		internal bool CanArm { get; init; }

		internal string Refusal { get; init; } = "no world";

		internal List<WorldCutReason> RequestedReasons { get; } = [];

		public event Action<WorldCutReport>? CutReported;

		public event Action<WorldRestoreReport>? RestoreReported;

		public bool IsEnabled => true;

		public bool HasArmedCut { get; private set; }

		public string CurrentWorldId => "w-test";

		/// <summary>Set by a test that needs the console-adjacent code to see an archive-owned generation.</summary>
		public bool RestoredGeneration { get; internal set; }

		public string? ContinueWorldId => null;

		public bool HasRestorableWorld => false;

		public IReadOnlyList<SavedCharacter> PendingCharacters => [];

		/// <summary>Every abandon the console-adjacent code performed (a dead continue attempt).</summary>
		internal List<string> Abandoned { get; } = [];

		public void AbandonRestore(string reason) => Abandoned.Add(reason);

		internal void Raise(WorldCutReport report) => CutReported?.Invoke(report);

		internal void RaiseRestore(WorldRestoreReport report) => RestoreReported?.Invoke(report);

		public bool TryBeginRun(bool isTutorial) => true;

		public bool TryRequestCut(WorldCutReason reason, out string? refusal)
		{
			if (!CanArm)
			{
				refusal = Refusal;
				return false;
			}

			refusal = null;
			RequestedReasons.Add(reason);
			HasArmedCut = true;
			return true;
		}

		public WorldCutReport? TryCaptureArmedCut(CharacterDataMsg? hostCharacter, int frame, IReadOnlyList<WorldTransientCount>? liveTransients = null)
		{
			HasArmedCut = false;
			return null;
		}

		public bool TryContinue(out WorldContinueOutcome outcome)
		{
			outcome = WorldContinueOutcome.Refused(string.Empty, "not supported", new SalvageResult(DamageReport.Empty));
			return false;
		}
	}
}
