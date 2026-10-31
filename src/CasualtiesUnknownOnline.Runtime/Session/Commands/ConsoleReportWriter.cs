using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The console's REPORT half: the lines the console prints for things that happened to this client rather than lines the player typed. A cut resolved after
/// <c>/save</c> returned, the Continue click's account, the host's answer to a member's <c>/give</c>, <c>/summon</c>, <c>/heal</c> or <c>/gamerule</c>, the write
/// verb's late verdict, a grant of starting supplies — each is a publisher's event, and each becomes one or more console lines here.
/// </summary>
/// <remarks>
/// Its own type for two reasons. The console service is the editor of the line and the owner of the panel; translating a report into lines is content, not
/// editing, and it changes for reasons the editor's work does not. And the service stood at 684 lines against a 600-line aggregate limit: the report family is
/// the cohesive block that leaves it.
///
/// <para>
/// It owns the subscriptions as well as the handlers, so the console has one thing to attach and one to detach. Every publisher is an event on a service the
/// console already holds; nothing here polls and nothing here owns state.
/// </para>
/// </remarks>
internal sealed class ConsoleReportWriter(IConsoleLineSink lines)
{
	private Action? _detach;

	/// <summary>Subscribes every report the console prints. The publishers are the console's own collaborators, handed in once and held by the detach action.</summary>
	internal void Attach(
		IWorldSaveControl saves,
		WorldRestoreAudit restoreAudit,
		IStartingSupplyControl startingSupplies,
		ItemStateSettlement settlement,
		IPlayerInteractionControl interaction,
		IWorldSummonControl summon,
		IHealResetControl heal,
		IHostRuleChangeControl rules)
	{
		saves.CutReported += OnCutReported;
		saves.RestoreReported += OnRestoreReported;
		restoreAudit.Reported += OnRestoreLiveWrite;
		startingSupplies.Reported += OnStartingSuppliesReported;
		settlement.Refused += OnItemDataWriteRefused;
		interaction.GiveAnswered += OnGiveAnswered;
		summon.Answered += OnSummonAnswered;
		heal.Answered += OnHealResetAnswered;
		heal.Applied += OnHealResetApplied;
		rules.Answered += OnGameRuleAnswered;

		_detach = () =>
		{
			saves.CutReported -= OnCutReported;
			saves.RestoreReported -= OnRestoreReported;
			restoreAudit.Reported -= OnRestoreLiveWrite;
			startingSupplies.Reported -= OnStartingSuppliesReported;
			settlement.Refused -= OnItemDataWriteRefused;
			interaction.GiveAnswered -= OnGiveAnswered;
			summon.Answered -= OnSummonAnswered;
			heal.Answered -= OnHealResetAnswered;
			heal.Applied -= OnHealResetApplied;
			rules.Answered -= OnGameRuleAnswered;
		};
	}

	/// <summary>Unsubscribes everything <see cref="Attach"/> subscribed. Safe to call twice.</summary>
	internal void Detach()
	{
		_detach?.Invoke();
		_detach = null;
	}

	/// <summary>
	/// A host answer that carries catalogue PARTS: each part is one line, rendered by THIS client's catalogue, so the member reads the answer in the member's
	/// language (the answer half of the localization rule). An answer with no parts — a shape the producing side has not converted yet — falls back to the text
	/// the host sent, which is why the text field stays.
	/// </summary>
	private void AddAnswerParts(IReadOnlyList<ConsoleAnswerPartMsg>? parts, string fallback, ConsoleLineKind kind)
	{
		if (parts is not { Count: > 0 })
		{
			lines.AddLine(fallback, kind, notifiable: true);
			return;
		}

		foreach (var part in parts)
		{
			lines.AddKeyedLine(part.Key, fallback, kind, [.. part.Args]);
		}
	}

	/// <summary>The cut the frame-end seam resolved — only the cuts the player asked for (a layer advance or an interval autosave is logged, not printed).</summary>
	private void OnCutReported(WorldCutReport report)
	{
		if (report.PlayerInitiated)
		{
			lines.AddLine(report.Describe(), report.Captured ? ConsoleLineKind.Success : ConsoleLineKind.Error, notifiable: true);
		}
	}

	/// <summary>
	/// The Continue click's account (§6: no silent loss). The DISPOSITION becomes the one notification the player is interrupted by, and the account itself — the
	/// one-line summary with its per-domain counts, then one line per skipped content id, refused claimant, unbindable native field and backup fallback — goes
	/// into the history behind it, where it can be read without pushing the player's other notices out of the closed console's newest-few window.
	/// </summary>
	private void OnRestoreReported(WorldRestoreReport report)
	{
		var headline = report.Result switch
		{
			WorldRestoreReport.Disposition.Refused => $"CUO continue refused: {report.Summary}",
			WorldRestoreReport.Disposition.Abandoned => $"CUO continue abandoned: {report.Summary}",
			_ when report.Clean => $"CUO restored world {report.WorldId}: nothing was lost.",
			_ => $"CUO restored world {report.WorldId} with {report.Details.Count} damaged item(s); the console history names them.",
		};
		lines.AddLine(headline, report.Clean ? ConsoleLineKind.Success : ConsoleLineKind.Error, notifiable: true);

		// The refusal and abandonment headlines ARE the account's one line; the applied ones are a short form of it, so only those add the summary to the history.
		if (report.Result == WorldRestoreReport.Disposition.Applied)
		{
			lines.AddLine(report.Summary, ConsoleLineKind.Info, notifiable: false);
		}

		foreach (var detail in report.Details)
		{
			lines.AddLine(detail, ConsoleLineKind.Info, notifiable: false);
		}
	}

	/// <summary>The live-world half of a restore: the world-entry seam wrote — or could not write — the restored facts.</summary>
	private void OnRestoreLiveWrite(WorldRestoreLiveWriteReport report) =>
		lines.AddLine(
			$"CUO restore of world {report.WorldId}: {report.Summary}",
			report.Complete ? ConsoleLineKind.Success : ConsoleLineKind.Error,
			notifiable: true);

	/// <summary>
	/// The host refused a console item-data write this client had already typed. The answer arrives after the verb's own line — the write is applied locally and
	/// reported, so the verdict is a round trip behind it — and it belongs in the console because that is where the operator is reading: the value they wrote is
	/// no longer there, and this is the only line that says so. Always an error line.
	/// </summary>
	private void OnItemDataWriteRefused(ItemDataWriteRefusal refusal) =>
		lines.AddLine(refusal.Describe(), ConsoleLineKind.Error, notifiable: true);

	/// <summary>The host's answer to this member's <c>/give</c>: the item, where it landed and the state it was given, printed where the verb was typed.</summary>
	private void OnGiveAnswered(PlayerInventoryGiveResultMsg msg) =>
		AddAnswerParts(msg.Parts, msg.Answer, msg.Applied ? ConsoleLineKind.Success : ConsoleLineKind.Error);

	/// <summary>The host's answer to this member's <c>/summon</c> — the created object and its point, or the refusal.</summary>
	private void OnSummonAnswered(WorldSummonResultMsg msg) =>
		AddAnswerParts(msg.Parts, msg.Answer, msg.Applied ? ConsoleLineKind.Success : ConsoleLineKind.Error);

	/// <summary>The host's answer to this member's <c>/heal</c> — what was reset, revived or restored, or the refusal that answered it.</summary>
	private void OnHealResetAnswered(HealResetResultMsg msg) =>
		AddAnswerParts(msg.Parts, msg.Answer, msg.Applied ? ConsoleLineKind.Success : ConsoleLineKind.Error);

	/// <summary>The host's answer to this member's <c>/gamerule</c> — the value the host's entry holds.</summary>
	private void OnGameRuleAnswered(HostRuleChangeResultMsg msg) =>
		AddAnswerParts(msg.Parts, msg.Answer, msg.Applied ? ConsoleLineKind.Success : ConsoleLineKind.Error);

	/// <summary>
	/// The host reset THIS client's body. A member's reset is never its own command's return — the host instructs, this client applies — so this notice is the only
	/// line on the player's screen that says what happened; an incomplete reset is announced as an error.
	/// </summary>
	private void OnHealResetApplied(HealResetNotice notice) =>
		lines.AddLine(notice.Text, notice.Outcome.Applied ? ConsoleLineKind.Success : ConsoleLineKind.Error, notifiable: true);

	/// <summary>
	/// The starting-supplies grant of a player this world had no character for: ONE line, the same words the log carries, so the player's screen and the log can be
	/// compared without translating. An incomplete grant is the failure shape and is announced as an error.
	/// </summary>
	private void OnStartingSuppliesReported(StartingSupplyGrantReport report) =>
		lines.AddKeyedLine(
			report.LineKey(),
			report.Describe(),
			report.Outcome == StartingSupplyGrantReport.Disposition.Granted && report.Complete
				? ConsoleLineKind.Success
				: report.Outcome == StartingSupplyGrantReport.Disposition.Granted
					? ConsoleLineKind.Error
					: ConsoleLineKind.Info,
			[.. report.LineArgs.Select(arg => arg?.ToString() ?? string.Empty)]);
}
