using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behaviour family: the give/use target of one drag, per the user's 2026-10-22 ruling — the player whose
/// detection range the cursor entered last and still occupies, the last target kept when the cursor leaves
/// every range (the path onto the displayed backpack), and a still cursor's overlapping ranges resolved by
/// distance. The type is pure so the rule is pinned without a scene: the adapter only feeds it positions.
/// </summary>
public class DragTargetSelectionTests
{
	private const float Radius = 1.5f;

	[Fact]
	public void TheFirstRangeEntered_BecomesTheTarget()
	{
		var selection = new DragTargetSelection();

		Frame(selection, 0f, 0f, (1, 0.5f, 0f));

		Assert.Equal(1ul, selection.Target);
		Assert.True(selection.IsTargetInRange);
	}

	[Fact]
	public void A_RangeEnteredWhileTheCurrentOneStillHoldsTheCursor_Wins()
	{
		var selection = new DragTargetSelection();
		Frame(selection, 0f, 0f, (1, 0.5f, 0f));

		Frame(selection, 0.8f, 0f, (1, 0.5f, 0f), (2, 1.0f, 0f));

		Assert.Equal(2ul, selection.Target);
		Assert.True(selection.IsTargetInRange);
	}

	[Fact]
	public void TheTargetFallsBackToTheRangeStillUnderTheCursor_WhenTheNewerOneIsLeft()
	{
		var selection = new DragTargetSelection();
		Frame(selection, 0f, 0f, (1, -1.0f, 0f));
		Frame(selection, 0f, 0f, (1, -1.0f, 0f), (2, 1.0f, 0f));
		Assert.Equal(2ul, selection.Target);

		Frame(selection, -1.4f, 0f, (1, -1.0f, 0f), (2, 1.0f, 0f));

		Assert.Equal(1ul, selection.Target);
		Assert.True(selection.IsTargetInRange);
	}

	[Fact]
	public void LeavingEveryRange_KeepsTheLastTarget_ButItNoLongerCountsAsInRange()
	{
		var selection = new DragTargetSelection();
		Frame(selection, 0f, 0f, (1, 0.5f, 0f));

		Frame(selection, 5f, 5f, (1, 0.5f, 0f));

		Assert.Equal(1ul, selection.Target);
		Assert.False(selection.IsTargetInRange);
	}

	[Fact]
	public void MovingOntoTheDisplayedBackpack_KeepsTheTargetToGiveTo()
	{
		var selection = new DragTargetSelection();
		Frame(selection, 0f, 0f, (1, 0.5f, 0f), (2, 8f, 8f));

		// The displayed backpack sits outside the detection circle: the give must still name player 1.
		Frame(selection, 3f, 0f, (1, 0.5f, 0f), (2, 8f, 8f));

		Assert.Equal(1ul, selection.Target);
		Assert.False(selection.IsTargetInRange);
	}

	[Fact]
	public void SimultaneousEntries_PickTheNearest_SoAStillCursorStaysPredictable()
	{
		var selection = new DragTargetSelection();

		Frame(selection, 0f, 0f, (1, 0.5f, 0f), (2, 0.7f, 0f));

		Assert.Equal(1ul, selection.Target);
	}

	[Fact]
	public void ReenteringARange_LaterInTheSameDrag_WinsAgain()
	{
		var selection = new DragTargetSelection();
		Frame(selection, 0f, 0f, (1, 0.5f, 0f));
		Frame(selection, 5f, 5f, (1, 0.5f, 0f), (2, 5.6f, 5f));
		Assert.Equal(2ul, selection.Target);

		Frame(selection, 5f, 5f, (1, 0.5f, 0f), (2, 5.6f, 5f));
		Frame(selection, 0f, 0f, (1, 0.5f, 0f), (2, 5.6f, 5f));

		Assert.Equal(1ul, selection.Target);
	}

	[Fact]
	public void APlayerNoLongerOffered_IsDroppedFromTheRanges()
	{
		var selection = new DragTargetSelection();
		Frame(selection, 0f, 0f, (1, 0.5f, 0f));

		Frame(selection, 0f, 0f, (2, 9f, 9f));

		Assert.Equal(1ul, selection.Target);
		Assert.False(selection.IsInRange(1));
		Assert.False(selection.IsTargetInRange);
	}

	[Fact]
	public void Begin_ClearsTheTargetAndEveryRange()
	{
		var selection = new DragTargetSelection();
		Frame(selection, 0f, 0f, (1, 0.5f, 0f));
		Assert.NotNull(selection.Target);

		selection.Begin();

		Assert.Null(selection.Target);
		Assert.False(selection.IsTargetInRange);
		Assert.False(selection.IsInRange(1));
	}

	private static void Frame(DragTargetSelection selection, float cursorX, float cursorY, params (ulong Id, float X, float Y)[] remotes)
	{
		selection.BeginFrame(cursorX, cursorY);
		foreach (var remote in remotes)
		{
			// The fixed-range form: a square of the old radius, which is what the cases above were written against.
			selection.Consider(remote.Id, remote.X, remote.Y, Radius);
		}

		selection.EndFrame();
	}

	/// <summary>
	/// The shape row (ticket `cross-player-drag-use-model-bounds.md`): the detection range is the MODEL's own box, not a
	/// fixed circle. Two cases pin the difference — the box refuses a cursor the old circle covered, and accepts one on
	/// the model's edge — and a third keeps the last-entered rule with its distance tie-break.
	/// </summary>
	[Fact]
	public void TheModelBox_DoesNotCountACursorTheOldCircleWouldHaveCovered()
	{
		var selection = new DragTargetSelection();
		selection.BeginFrame(1.2f, 0f); // inside the old 1.5 circle, outside the 0.4-wide model box
		selection.Consider(1ul, 0f, 0f, halfWidth: 0.4f, halfHeight: 0.9f);
		selection.EndFrame();

		Assert.Null(selection.Target);
		Assert.False(selection.IsTargetInRange);
	}

	[Fact]
	public void TheModelBox_CountsACursorOnTheModelsEdge()
	{
		var selection = new DragTargetSelection();
		selection.BeginFrame(0.55f, 0f);
		selection.Consider(1ul, 0f, 0f, halfWidth: 0.4f, halfHeight: 0.9f);
		selection.EndFrame();

		Assert.Null(selection.Target); // 0.55 > 0.4: the box decides, not the circle

		selection.BeginFrame(0.35f, 0.8f);
		selection.Consider(1ul, 0f, 0f, halfWidth: 0.4f, halfHeight: 0.9f);
		selection.EndFrame();

		Assert.Equal(1ul, selection.Target);
	}

	[Fact]
	public void TheModelBox_KeepsTheLastEnteredRuleAndItsDistanceTieBreak()
	{
		var selection = new DragTargetSelection();
		selection.BeginFrame(0f, 0f);
		selection.Consider(1ul, 0.2f, 0f, 0.4f, 0.9f);
		selection.Consider(2ul, 0.3f, 0f, 0.4f, 0.9f);
		selection.EndFrame();

		// Both boxes hold the cursor on the same frame: nearest wins, exactly as the circle cases pin.
		Assert.Equal(1ul, selection.Target);
	}
}
