using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Random = UnityEngine.Random;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The Game Adapter's answer to <see cref="IShrapnelGripSlipRandom"/>: the engine's own
/// <c>UnityEngine.Random</c> stream, held on a state derived from the slip's seed.
/// <para>
/// The draw script below is the native <c>ShrapnelMinigame.BreakGrasp</c>'s own read order, reproduced
/// so the authority draws what the operator's screen drew:
/// </para>
/// <list type="number">
/// <item><c>Random.Range(4f, 6f)</c> — skin (<c>ShrapnelMinigame.cs:69</c>).</item>
/// <item><c>Random.Range(0.4f, 1f)</c> — bleed (<c>:70</c>).</item>
/// <item><c>Random.Range(1, 6)</c> — the gore clip index inside <c>Body.DoGoreSound</c>
/// (<c>Body.cs:2445</c>). The value is discarded here; only its place in the stream matters.</item>
/// <item><c>Random.Range(0.85f, 1.15f)</c> — the pitch shift <c>Sound.Play</c> rolls because
/// <c>DoGoreSound</c> passes <c>pitchShift: true</c> with the default pitch of <c>1f</c>
/// (<c>Sound.cs:17</c>). Discarded for the same reason.</item>
/// <item><c>Random.Range(9f, 16f)</c> — pain (<c>ShrapnelMinigame.cs:72</c>).</item>
/// <item>For a head limb only: <c>Random.value</c> for the <c>0.8</c> gate and, when it passes, a
/// second <c>Random.value</c> for the brain loss (<c>:73-76</c>).</item>
/// </list>
/// <para>
/// Steps 3 and 4 are the reason this script exists at all rather than a bare pair of calls: the native
/// call rolls two values nobody keeps, and a script that skipped them would hand the authority the
/// operator's skin and bleed but a different pain. They are placeholders by necessity — the authority
/// must not PLAY the gore sound for a slip the operator already heard — so each is written as the same
/// engine call the native code makes, which is what keeps the position in the stream identical.
/// <see cref="ShrapnelGripSlipRoller"/> compares the two sides' values at runtime and logs a
/// disagreement, which is how a future game update that changes this order becomes visible instead of
/// silently re-splitting the two screens.
/// </para>
/// <para>
/// The window is short and synchronous on both sides — the operator's native <c>BreakGrasp</c> call and
/// the authority's draw above — so holding the engine's one global stream for its duration cannot
/// collide with another consumer. The stream is restored before either returns.
/// </para>
/// </summary>
internal sealed class EngineShrapnelGripSlipRandom : IShrapnelGripSlipRandom
{
	public ShrapnelGripSlipValues? TryDraw(int seed, bool isHead)
	{
		var saved = Random.state;
		Random.InitState(seed);
		var values = DrawScript(isHead);
		Random.state = saved;
		return values;
	}

	public IShrapnelGripSlipWindow? TryEnterWindow(int seed)
	{
		var saved = Random.state;
		Random.InitState(seed);
		return new Window(saved);
	}

	/// <summary>The native read order, value for value; see the type's own remarks for the call sites.</summary>
	private static ShrapnelGripSlipValues DrawScript(bool isHead)
	{
		var skin = Random.Range(4f, 6f);
		var bleed = Random.Range(0.4f, 1f);
		_ = Random.Range(1, 6);
		_ = Random.Range(0.85f, 1.15f);
		var pain = Random.Range(9f, 16f);
		var brain = 0f;
		if (isHead && Random.value < 0.8f)
		{
			brain = Random.value;
		}

		return new ShrapnelGripSlipValues(skin, bleed, pain, brain);
	}

	/// <summary>
	/// One held-open engine stream. <see cref="Dispose"/> is idempotent because two callers close it:
	/// the patch's postfix on the normal path and its finalizer when the native call throws, where the
	/// postfix never runs. Either way the stream goes back to where it was.
	/// </summary>
	private sealed class Window(Random.State saved) : IShrapnelGripSlipWindow
	{
		private readonly Random.State _saved = saved;
		private bool _closed;

		public void Dispose()
		{
			if (_closed)
			{
				return;
			}

			_closed = true;
			Random.state = _saved;
		}
	}
}
