using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A stand-in for the engine random stream the shrapnel grip-slip window is derived from. It records
/// every seed it was asked for, and its <see cref="Script"/> is the deterministic draw both halves
/// share — so a test can predict the authority's numbers, or pin them outright with
/// <see cref="NextDraw"/>.
/// <para>
/// <see cref="Available"/> set to false models the base composition root's answer
/// (<c>NoShrapnelGripSlipRandom</c>): no stream, no window, and the authority refuses the slip.
/// </para>
/// </summary>
internal sealed class FakeShrapnelGripSlipRandom : IShrapnelGripSlipRandom
{
	/// <summary>What the next <see cref="TryDraw"/> returns; null uses <see cref="Script"/>.</summary>
	internal ShrapnelGripSlipValues? NextDraw { get; set; }

	/// <summary>False models a build with no engine stream to derive from.</summary>
	internal bool Available { get; set; } = true;

	internal List<int> DrawnSeeds { get; } = [];

	/// <summary>The head flag each draw was asked for, in order — the only way a test can pin that the caller passes it through.</summary>
	internal List<bool> DrawnHeadFlags { get; } = [];

	internal List<int> WindowSeeds { get; } = [];

	public ShrapnelGripSlipValues? TryDraw(int seed, bool isHead)
	{
		DrawnSeeds.Add(seed);
		DrawnHeadFlags.Add(isHead);
		return Available ? NextDraw ?? Script(seed, isHead) : null;
	}

	public IShrapnelGripSlipWindow? TryEnterWindow(int seed)
	{
		WindowSeeds.Add(seed);
		return Available ? new Window() : null;
	}

	/// <summary>
	/// The deterministic script both halves of the mechanism share, with the same shape as the native
	/// draw: every value strictly positive, so a test can tell "applied" from "applied nothing".
	/// </summary>
	internal static ShrapnelGripSlipValues Script(int seed, bool isHead)
	{
		var random = new Random(seed);
		var skin = 4f + ((float)random.NextDouble() * 2f);
		var bleed = 0.4f + ((float)random.NextDouble() * 0.6f);
		var pain = 9f + ((float)random.NextDouble() * 7f);
		var brain = isHead && random.NextDouble() < 0.8 ? (float)random.NextDouble() : 0f;
		return new ShrapnelGripSlipValues(skin, bleed, pain, brain);
	}

	private sealed class Window : IShrapnelGripSlipWindow
	{
		public void Dispose()
		{
		}
	}
}
