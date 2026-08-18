using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// #195 blueprint popup: the recipe-unlock fact (RecipeUnlockMsg) already
/// reaches every side, but only the acting player saw the game's native
/// "learned recipe" popup. The apply shell now shows the popup for a NEW
/// unlock and skips it for an already-learned recipe (the acting side already
/// showed it natively). These tests lock the pure decision/text logic; the
/// adapter is compile-excluded, so the surface is exercised reflectively.
/// </summary>
public class RecipeUnlockPopupTests
{
	private static readonly Type Apply = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Items.RecipeUnlockApply",
		throwOnError: true)!;

	[Theory]
	[InlineData(1, true)]
	[InlineData(2, true)]
	[InlineData(0, false)]
	public void ShouldShowPopup_OnlyForANewLearn(int previousInt, bool expected)
	{
		var method = Apply.GetMethod("ShouldShowPopup", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("RecipeUnlockApply.ShouldShowPopup not found.");

		Assert.Equal(expected, (bool)method.Invoke(null, [previousInt])!);
	}

	[Fact]
	public void BuildPopupText_ReplacesTheItemPlaceholder()
	{
		var method = Apply.GetMethod("BuildPopupText", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("RecipeUnlockApply.BuildPopupText not found.");

		var text = (string)method.Invoke(null, ["You learned r1!", "Bandage"])!;
		Assert.Equal("You learned Bandage!", text);
	}

	[Fact]
	public void ApplyType_KeepsTheNewlyUnlockedPopupPath()
	{
		var popup = Apply.GetMethod("ShowNewlyUnlockedPopup", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("RecipeUnlockApply.ShowNewlyUnlockedPopup not found.");
		var receive = Apply.GetMethod("OnRecipeUnlockReceived", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("RecipeUnlockApply.OnRecipeUnlockReceived not found.");

		Assert.Single(receive.GetParameters());
		Assert.Equal(2, popup.GetParameters().Length);
	}
}
