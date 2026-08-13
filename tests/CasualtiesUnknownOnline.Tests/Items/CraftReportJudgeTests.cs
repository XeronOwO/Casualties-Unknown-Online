using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The craft-report classification (pure): the host's world table and the
/// sender's transfer-table membership decide what each entry means to the
/// apply. Destroyed → where the item lives decides the removal; Changed →
/// world vs carried decides the adopt path. Unbound ids (0) are skipped.
/// </summary>
public class CraftReportJudgeTests
{
	private static CraftEntryMsg Destroyed(ulong id) =>
		new() { Disposition = CraftEntryDisposition.Destroyed, Item = new CharacterItemMsg { InstanceId = id, ItemId = "x" } };

	private static CraftEntryMsg Changed(ulong id) =>
		new() { Disposition = CraftEntryDisposition.Changed, Item = new CharacterItemMsg { InstanceId = id, ItemId = "x", Condition = 0.5f } };

	private static List<(ulong ItemId, CraftVerdict Verdict)> Classify(
		CraftReportMsg msg, params ulong[] worldIds) => CraftReportJudge.Classify(msg, [.. worldIds], []);

	[Fact]
	public void Destroyed_WorldItem_ClassifiesWorldDestroy()
	{
		var verdicts = Classify(new CraftReportMsg { Entries = [Destroyed(42)] }, 42);

		Assert.Single(verdicts);
		Assert.Equal(CraftVerdict.WorldDestroy, verdicts[0].Verdict);
	}

	[Fact]
	public void Destroyed_TransferredItem_ClassifiesTransferredRemove()
	{
		var verdicts = CraftReportJudge.Classify(new CraftReportMsg { Entries = [Destroyed(42)] }, [], [42]);

		Assert.Single(verdicts);
		Assert.Equal(CraftVerdict.TransferredRemove, verdicts[0].Verdict);
	}

	[Fact]
	public void Destroyed_UnknownItem_ClassifiesUnknownSkip()
	{
		var verdicts = Classify(new CraftReportMsg { Entries = [Destroyed(42)] });

		Assert.Single(verdicts);
		Assert.Equal(CraftVerdict.UnknownSkip, verdicts[0].Verdict);
	}

	[Fact]
	public void Changed_WorldItem_ClassifiesWorldChange()
	{
		var verdicts = Classify(new CraftReportMsg { Entries = [Changed(42)] }, 42);

		Assert.Single(verdicts);
		Assert.Equal(CraftVerdict.WorldChange, verdicts[0].Verdict);
	}

	[Fact]
	public void Changed_TransferredItem_ClassifiesAdoptChange()
	{
		var verdicts = CraftReportJudge.Classify(new CraftReportMsg { Entries = [Changed(42)] }, [], [42]);

		Assert.Single(verdicts);
		Assert.Equal(CraftVerdict.AdoptChange, verdicts[0].Verdict);
	}

	[Fact]
	public void Changed_UntrackedItem_ClassifiesAdoptChange()
	{
		// The apply's untracked fallback registers the report as the fact
		// (the use-path philosophy) — the classification itself is the same.
		var verdicts = Classify(new CraftReportMsg { Entries = [Changed(42)] });

		Assert.Single(verdicts);
		Assert.Equal(CraftVerdict.AdoptChange, verdicts[0].Verdict);
	}

	[Fact]
	public void UnboundEntry_IsSkipped()
	{
		var verdicts = Classify(new CraftReportMsg { Entries = [Destroyed(0)] });

		Assert.Empty(verdicts);
	}

	[Fact]
	public void EmptyReport_ClassifiesNothing() => Assert.Empty(Classify(new CraftReportMsg()));

	[Fact]
	public void MixedEntries_ClassifyIndependently()
	{
		var verdicts = CraftReportJudge.Classify(
			new CraftReportMsg { Entries = [Destroyed(1), Destroyed(2), Destroyed(3), Changed(4), Changed(5)] },
			[1, 4], [2, 5]);

		Assert.Equal(5, verdicts.Count);
		Assert.Equal(CraftVerdict.WorldDestroy, verdicts[0].Verdict);
		Assert.Equal(CraftVerdict.TransferredRemove, verdicts[1].Verdict);
		Assert.Equal(CraftVerdict.UnknownSkip, verdicts[2].Verdict);
		Assert.Equal(CraftVerdict.WorldChange, verdicts[3].Verdict);
		Assert.Equal(CraftVerdict.AdoptChange, verdicts[4].Verdict);
	}
}
