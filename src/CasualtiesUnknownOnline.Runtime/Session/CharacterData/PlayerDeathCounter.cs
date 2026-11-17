namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Counts how many times THIS client's own character has died in the current run — the owner's half of the
/// per-player death count (the user's decision 2026-11-14). The edge is the game's own flag,
/// <c>Body.alive</c> = <c>brainHealth &gt; 0f</c> (release 1.0.2, <c>Body.cs:207-216</c>), never a damage
/// estimate; the adapter feeds it one frame at a time, and the count rides the character snapshot the client
/// already reports, so the host's store has it, the cut persists it and a restore writes it back.
///
/// <para>
/// The edge is armed only by a body that has been seen alive. A fresh spawn, a restore and a world entry all
/// begin alive (or without a body at all), so none of them is a death; <see cref="SetCount"/> then REPLACES
/// the count with the character's saved value and re-arms, so the restore's own spawn is not counted either.
/// </para>
/// </summary>
public sealed class PlayerDeathCounter
{
	private bool _armed;
	private int _count;

	/// <summary>The deaths this client has counted for its own character in this run.</summary>
	public int Count => _count;

	/// <summary>
	/// One frame of the local body: <c>null</c> = there is no local body this frame (the edge is disarmed —
	/// a body that was not there cannot have died), true = armed, false while armed = one death.
	/// </summary>
	public void Tick(bool? alive)
	{
		if (alive is not { } aliveNow)
		{
			_armed = false;
			return;
		}

		if (aliveNow)
		{
			_armed = true;
			return;
		}

		if (_armed)
		{
			_armed = false;
			_count++;
		}
	}

	/// <summary>A restored snapshot names the character's count: write it and disarm, so the spawn that the restore itself caused is not a death.</summary>
	public void SetCount(int count)
	{
		_count = count > 0 ? count : 0;
		_armed = false;
	}

	/// <summary>A new run starts a new character at zero; the first alive frame arms the edge without counting.</summary>
	public void Reset()
	{
		_count = 0;
		_armed = false;
	}
}
