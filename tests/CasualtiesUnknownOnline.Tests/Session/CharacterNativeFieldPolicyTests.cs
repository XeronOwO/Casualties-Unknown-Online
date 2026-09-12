using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// S3.4b's native CHARACTER fields: the rules a snapshot of them has to obey.
/// <c>CharacterNativeFieldPolicy</c> decides whether one read of the live scene
/// describes a whole character (<c>TryCapture</c>), what a restore may write and
/// what it must refuse by name (<c>Plan</c>), and what a restore report says about
/// a snapshot whose fields cannot all be put back (<c>Missing</c>).
///
/// The adapter performs the reads and writes a plan describes; these tests pin the
/// accounting, which is what a restore report and a stored archive depend on: a
/// value the native contract cannot hold must be a NAMED refusal, never a
/// truncation, a prefix write or a silent default.
/// </summary>
public sealed class CharacterNativeFieldPolicyTests
{
	[Fact]
	public void TryCapture_AWholeReadOfTheLiveScene_Succeeds()
	{
		Assert.True(CharacterNativeFieldPolicy.TryCapture(
			happinessLength: CharacterNativeFieldPolicy.HappinessWindowLength,
			liveScene: true,
			characterInfoLength: CharacterNativeFieldPolicy.CharacterDetailCount,
			out var failure));

		Assert.Null(failure);
	}

	[Theory]
	[InlineData(false, 10, 4, "player camera or wound window")] // no live scene at all
	[InlineData(true, 0, 4, "not the game's 10")] // a body without its window
	[InlineData(true, 1, 4, "not the game's 10")] // an unfinished history — the game reads all ten slots
	[InlineData(true, 9, 4, "not the game's 10")]
	[InlineData(true, 11, 4, "not the game's 10")]
	[InlineData(true, 10, 0, "not the native 4")] // a window whose details were never filled
	[InlineData(true, 10, 5, "not the native 4")] // a malformed row
	public void TryCapture_AnIncoherentRead_ReportsWhyItFailed(bool liveScene, int happinessLength, int detailCount, string expected)
	{
		// The refusal has to say WHAT was missing: a capture that returned zeros
		// instead would be written into the archive as a real value (a new
		// character's window IS 0 cm / 0 y / #0).
		Assert.False(CharacterNativeFieldPolicy.TryCapture(happinessLength, liveScene, detailCount, out var failure));

		Assert.Contains(expected, failure, StringComparison.Ordinal);
	}

	[Fact]
	public void Plan_AWholeSnapshot_PlansEveryFieldInOrder()
	{
		var data = new CharacterDataMsg { NativeFields = Fields() };

		var plan = CharacterNativeFieldPolicy.Plan(data);

		Assert.Equal(
			[NativeFieldKind.HappinessHistory, NativeFieldKind.CaloriesConsumed, NativeFieldKind.CharacterInfo],
			plan.ConvertAll(write => write.Kind));
		Assert.All(plan, write => Assert.True(write.Applied));
		Assert.All(plan, write => Assert.Null(write.Refusal));
		Assert.Contains("caloriesConsumed (77)", plan[1].Description, StringComparison.Ordinal);
	}

	[Fact]
	public void Plan_SnapshotWithoutNativeFields_PlansNothing() =>
		// The whole absence is named once by Missing; three unnamed skips would be
		// worse than useless in the report.
		Assert.Empty(CharacterNativeFieldPolicy.Plan(new CharacterDataMsg()));

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(9)]
	public void Plan_HappinessHistoryShorterThanTheGameWindow_IsRefusedByName(int count)
	{
		// A prefix write would leave the rest of the ten-value window at the fresh
		// body's values, and the game reads ALL of them: AverageHappiness averages
		// the whole window (Body.cs:643-650) and the last-chance evaluation reads
		// slot 9 (Body.cs:957). So a short row is broken, not partially usable.
		var plan = CharacterNativeFieldPolicy.Plan(DataWithHappiness(count));

		var happiness = plan[0];
		Assert.False(happiness.Applied);
		Assert.Contains($"the game's 10-value window", happiness.Description, StringComparison.Ordinal);
		Assert.Contains($"not the game's whole 10-value window", happiness.Refusal, StringComparison.Ordinal);
		Assert.True(plan[1].Applied); // the other two fields are unaffected
		Assert.True(plan[2].Applied);
	}

	[Fact]
	public void Plan_HappinessHistoryLongerThanTheGameWindow_IsRefusedByName()
	{
		// The live array is the game's own window and its updater shifts that same
		// array in place, so a longer row cannot be written — truncating it silently
		// would lose the older half of the history without telling anyone.
		var plan = CharacterNativeFieldPolicy.Plan(DataWithHappiness(CharacterNativeFieldPolicy.HappinessWindowLength + 1));

		var happiness = plan[0];
		Assert.False(happiness.Applied);
		Assert.Contains("not the game's 10-value window", happiness.Description, StringComparison.Ordinal);
		Assert.Contains("not the game's whole 10-value window", happiness.Refusal, StringComparison.Ordinal);
	}

	[Fact]
	public void Plan_EmptyHappinessHistory_IsRefusedByName()
	{
		var plan = CharacterNativeFieldPolicy.Plan(DataWithHappiness(0));

		Assert.False(plan[0].Applied);
		Assert.Contains("carry no happiness history", plan[0].Refusal, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(3)]
	[InlineData(5)]
	public void Plan_CharacterDetailsNotTheNativeFour_AreRefusedByName(int count)
	{
		var data = Fields();
		data.CharacterInfo = [.. new int[count]];

		var plan = CharacterNativeFieldPolicy.Plan(new CharacterDataMsg { NativeFields = data });

		Assert.False(plan[2].Applied);
		Assert.Contains("not the native 4", plan[2].Refusal, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Plan_AFieldSerializedAsNull_IsRefusedByName(bool happiness) =>
		// A hand-edited or corrupted character file can carry an explicit null where a
		// list belongs (protobuf never does, JSON does). That is a value to refuse, not
		// a NullReferenceException in the middle of a restore — the restore's second
		// pass would then never clear its phase.
		Assert.False(CharacterNativeFieldPolicy.Plan(new CharacterDataMsg
		{
			NativeFields = new CharacterNativeFieldsMsg
			{
				LastHappiness = happiness ? null! : Fields().LastHappiness,
				CaloriesConsumed = 1,
				CharacterInfo = happiness ? [170, 20, 7, 2] : null!,
			},
		})[happiness ? 0 : 2].Applied);

	[Fact]
	public void Missing_NamesTheWholeAbsence_AndNothingWhenEveryFieldIsUsable()
	{
		var named = CharacterNativeFieldPolicy.Missing("steam-1001", new CharacterDataMsg());
		var silent = CharacterNativeFieldPolicy.Missing("steam-1001", new CharacterDataMsg { NativeFields = Fields() });

		var damage = Assert.Single(named);
		Assert.Contains("steam-1001", damage, StringComparison.Ordinal);
		Assert.Contains("lastHappiness, caloriesConsumed, WoundView.cInfo", damage, StringComparison.Ordinal);
		Assert.Contains("the game's defaults", damage, StringComparison.Ordinal);
		Assert.Empty(silent);
	}

	[Fact]
	public void Missing_NamesTheFieldsAPartlyUsableSnapshotCannotRestore()
	{
		// Non-null but malformed is exactly as much damage as absent: the report says
		// which field cannot be put back instead of treating the whole sub-message as
		// good news.
		var data = new CharacterDataMsg
		{
			NativeFields = new CharacterNativeFieldsMsg
			{
				LastHappiness = [0.5f],
				CaloriesConsumed = 5,
				CharacterInfo = [170, 20, 7, 2],
			},
		};

		var damage = Assert.Single(CharacterNativeFieldPolicy.Missing("steam-1001", data));

		Assert.Contains("steam-1001", damage, StringComparison.Ordinal);
		Assert.Contains("the happiness history", damage, StringComparison.Ordinal);
		Assert.DoesNotContain("WoundView.cInfo", damage, StringComparison.Ordinal);
	}

	private static CharacterDataMsg DataWithHappiness(int count) => new()
	{
		NativeFields = new CharacterNativeFieldsMsg
		{
			LastHappiness = [.. new float[count]],
			CaloriesConsumed = 5,
			CharacterInfo = [170, 20, 7, 2],
		},
	};

	private static CharacterNativeFieldsMsg Fields() => new()
	{
		LastHappiness = [.. new List<float> { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f }],
		CaloriesConsumed = 77,
		CharacterInfo = [170, 20, 7, 2],
	};
}
