using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// One grip slip the operator's own client is drawing: the derived engine window the native
/// <c>ShrapnelMinigame.BreakGrasp</c> runs inside, the limb fields that call is about to move, and the
/// nonce both machines derive the window's state from.
/// <para>
/// The values the report carries are read back off the DISPLAYED limb as the difference the native call
/// made, rather than from a second run of the draw script: the screen is what this operator sees, so the
/// numbers that describe it are the ones this client should be accountable for, and reading them costs
/// no extra draw. The read happens while the window is still open and inside the same call, so no frame
/// boundary and no report can have moved the limb in between.
/// </para>
/// <para>
/// The window is optional on purpose. A build that registers no engine stream
/// (<c>NoShrapnelGripSlipRandom</c>) still gets a slip object, with a nonce of zero: the report then
/// says "this client held no window", which is the honest description of a screen that drew from the
/// free-running stream, and the authority logs it instead of comparing against nothing.
/// </para>
/// </summary>
internal sealed class RemoteShrapnelGripSlip
{
	private readonly IShrapnelGripSlipWindow? _window;
	private readonly Limb? _limb;
	private readonly float _skinBefore;
	private readonly float _bleedBefore;
	private readonly float _painBefore;
	private readonly float _brainBefore;
	private bool _closed;

	internal RemoteShrapnelGripSlip(IShrapnelGripSlipWindow? window, Limb? limb, int pieceIndex, int nonce)
	{
		_window = window;
		_limb = limb;
		PieceIndex = pieceIndex;
		Nonce = nonce;
		_skinBefore = limb == null ? 0f : limb.skinHealth; // Unity object — ==
		_bleedBefore = limb == null ? 0f : limb.bleedAmount; // Unity object — ==
		_painBefore = limb == null ? 0f : limb.pain; // Unity object — ==
		_brainBefore = limb == null || limb.body == null ? 0f : limb.body.brainHealth; // Unity objects — ==
	}

	/// <summary>The native minigame slot the operator lost its grip on.</summary>
	internal int PieceIndex { get; }

	/// <summary>This client's own slip counter for the session, or 0 when it held no derived window.</summary>
	internal int Nonce { get; }

	/// <summary>The four values the native call just moved the displayed limb by; all-zero when the slip carried no window or no limb.</summary>
	internal ShrapnelGripSlipValues ReadValues()
	{
		var limb = _limb;
		if (limb == null) // Unity object — ==
		{
			return default;
		}

		var body = limb.body;
		var brain = body == null ? 0f : body.brainHealth; // Unity object — ==
		return new ShrapnelGripSlipValues(
			_skinBefore - limb.skinHealth,
			limb.bleedAmount - _bleedBefore,
			limb.pain - _painBefore,
			_brainBefore - brain);
	}

	/// <summary>
	/// Puts the engine stream back. Idempotent because two callers close the same slip: the patch's
	/// postfix on the normal path and its finalizer when the native call throws, where the postfix
	/// never runs.
	/// </summary>
	internal void Close()
	{
		if (_closed)
		{
			return;
		}

		_closed = true;
		_window?.Dispose();
	}
}
