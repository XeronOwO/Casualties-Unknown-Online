using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Who the native wound window's identity header belongs to, while a remote medical focus is
/// open. The header (<c>WoundView.bodyStatText</c>, <c>versionText</c> and the four ints behind
/// them) is written by exactly one native method, <c>WoundView.SetCharDetails</c>
/// (<c>WoundView.cs:54-68</c>), and the game calls it exactly once per character — at
/// <c>PlayerCamera.Start</c> with a fresh random roll, or through <c>SaveSystem</c> with the run
/// save's values (<c>PlayerCamera.cs:726-729</c>, <c>SaveSystem.cs:438-441</c>). Nothing rewrites
/// it afterwards, so a remote focus that does not borrow it leaves the VIEWER's identity over the
/// viewed player's body.
///
/// <para>
/// Three facts live here, and they are one rule: while the focus is open the panel shows the
/// viewed player's values; the viewer's own four values are remembered because the panel is their
/// ONLY carrier in the live game (<c>CharacterNativeFields.LiveSystem</c> reads it for the 1 Hz
/// snapshot, so a borrowed panel must answer with the remembered copy); and a writer that
/// re-authors the viewer's identity mid-focus (the local character restore) updates the remembered
/// copy instead of the borrowed panel.
/// </para>
///
/// <para>
/// A borrow is only good while the panel still carries what this focus left on it: the game, or a
/// reloaded scene, may have written the header in the meantime, and restoring the remembered values
/// over THAT write would put one character's identity on another's panel. Every read, adopt and
/// close therefore proves the panel still matches the borrow first, and a mismatch drops the
/// borrow instead of writing over the newer value. Unity-free, so the whole rule is a set of cases
/// (<c>RemoteMedicalHeaderTests</c>); the adapter half that performs the writes is
/// <see cref="RemoteMedicalHeader"/>.
/// </para>
/// </summary>
internal sealed class RemoteMedicalHeaderState
{
	/// <summary>What one refresh asks the native wound window's identity header to show.</summary>
	internal enum WriteKind
	{
		/// <summary>Nothing: the header already shows what this focus wants it to show.</summary>
		None,

		/// <summary>Write four values through the native <c>SetCharDetails</c>.</summary>
		Show,

		/// <summary>Blank the two labels: the viewed player's identity is not known, and the viewer's own must not stand in for it.</summary>
		Blank,
	}

	/// <summary>One refresh's answer, carrying the values to write when the kind is <see cref="WriteKind.Show"/>.</summary>
	internal readonly record struct Write(WriteKind Kind, int[] Values)
	{
		internal static readonly Write None = new(WriteKind.None, []);

		internal static readonly Write Blank = new(WriteKind.Blank, []);

		internal static Write Show(int[] values) => new(WriteKind.Show, values);
	}

	private const int DetailCount = CharacterNativeFieldPolicy.CharacterDetailCount;

	/// <summary>The viewer's own identity, remembered from the panel at the moment the focus borrowed it; null = not borrowed.</summary>
	private int[]? _own;

	/// <summary>The four values this focus last wrote through the native entry; null while it has written none (the panel still carries the viewer's own values, which is what the borrow compares against).</summary>
	private int[]? _shown;

	/// <summary>Whether this focus already blanked the labels: a second unknown refresh must not rewrite them.</summary>
	private bool _blanked;

	/// <summary>One refresh of the open focus: what to write, if anything.</summary>
	internal Write Refresh(int[] panelInfo, IReadOnlyList<int>? viewed)
	{
		if (_own is not null && !Matches(panelInfo))
		{
			// Something else wrote the header while this focus held it (a reloaded scene's own
			// SetCharDetails, a restore that bypassed the adopt seam): the remembered copy describes
			// a panel that no longer exists. Drop it and borrow the panel as it is now.
			Reset();
		}

		_own ??= [.. panelInfo];

		if (viewed is null || viewed.Count != DetailCount)
		{
			if (_blanked)
			{
				return Write.None;
			}

			// The labels only: cInfo keeps whatever this focus last wrote (a blank never touches it),
			// which is exactly what Matches must keep comparing the panel against.
			_blanked = true;
			return Write.Blank;
		}

		var values = new int[DetailCount];
		for (var i = 0; i < DetailCount; i++)
		{
			values[i] = viewed[i];
		}

		if (!_blanked && _shown is not null && _shown.SequenceEqual(values))
		{
			return Write.None;
		}

		_blanked = false;
		_shown = values;
		return Write.Show(values);
	}

	/// <summary>
	/// The viewer's own identity, whichever object currently carries it: the remembered copy while a
	/// focus holds the panel, the panel's own values otherwise. A stale borrow (the panel no longer
	/// shows what this focus left) is dropped, and the panel answers for itself.
	/// </summary>
	internal int[] OwnIdentity(int[] panelInfo)
	{
		if (_own is null)
		{
			return panelInfo;
		}

		if (!Matches(panelInfo))
		{
			Reset();
			return panelInfo;
		}

		return _own;
	}

	/// <summary>
	/// The viewer's own identity was re-authored while the panel is borrowed: remember the new
	/// values (the close writes them) and answer true so the caller does NOT write the borrowed
	/// panel. False = no focus holds the panel, or the borrow is stale — the caller writes normally.
	/// </summary>
	internal bool TryAdoptOwn(int[] panelInfo, IReadOnlyList<int> details)
	{
		if (_own is null || details.Count != DetailCount || !Matches(panelInfo))
		{
			return false;
		}

		_own = [.. details];
		return true;
	}

	/// <summary>
	/// The viewer's own four values while a focus holds the panel and the panel still matches the
	/// borrow, or null otherwise. A native reader that assumes the header describes the local
	/// character (the game's own save, `SaveSystem.cs:172-175`) must be shown this instead of the
	/// borrowed values for the duration of its read.
	/// </summary>
	internal int[]? OwnIdentityForCapture(int[] panelInfo) =>
		_own is not null && Matches(panelInfo) ? _own : null;

	/// <summary>
	/// What to put the panel back to after a temporary capture of the viewer's own values. The four
	/// values are always re-written when this focus ever wrote them (the capture replaced cInfo too,
	/// not only the labels), and <see cref="LabelsBlank"/> says the labels must be blanked again
	/// afterwards.
	/// </summary>
	internal Write Reassert() =>
		_shown is not null ? Write.Show(_shown) : _blanked ? Write.Blank : Write.None;

	/// <summary>Whether the labels are blank right now — after a capture window wrote the values back, they must be blanked again.</summary>
	internal bool LabelsBlank => _blanked;

	/// <summary>
	/// Close the borrow: the viewer's own values to write back, or null when there is nothing to
	/// give back — no borrow, the panel is gone, or the panel no longer shows what this focus left
	/// on it (that newer write stands). Every call clears the state.
	/// </summary>
	internal int[]? End(int[]? panelInfo)
	{
		var own = _own is not null && panelInfo is not null && Matches(panelInfo) ? _own : null;
		Reset();
		return own;
	}

	private bool Matches(int[] panelInfo) =>
		_shown is not null
			? panelInfo.SequenceEqual(_shown)
			: _own is not null && panelInfo.SequenceEqual(_own);

	private void Reset()
	{
		_own = null;
		_shown = null;
		_blanked = false;
	}
}
