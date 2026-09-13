using System;
using System.Collections.Generic;
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
/// one inside the console callback — and the two deferred answers the seam
/// produces (the cut report and a restore's live-write report) are printed where
/// the player is already reading. A cut nobody asked for (a layer advance) stays
/// in the log.
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

		public bool IsEnabled => true;

		public bool HasArmedCut { get; private set; }

		public string CurrentWorldId => "w-test";

		public string? ContinueWorldId => null;

		public bool HasRestorableWorld => false;

		public IReadOnlyList<SavedCharacter> PendingCharacters => [];

		/// <summary>Every abandon the console-adjacent code performed (a dead continue attempt).</summary>
		internal List<string> Abandoned { get; } = [];

		public void AbandonRestore(string reason) => Abandoned.Add(reason);

		internal void Raise(WorldCutReport report) => CutReported?.Invoke(report);

		public bool TryBeginRun() => true;

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
