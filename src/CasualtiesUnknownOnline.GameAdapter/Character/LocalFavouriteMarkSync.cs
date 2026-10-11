using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The local favourite marks' adapter face: the ONE capture every character report
/// goes through, the seed a restored character brings back, and the reset a session
/// end performs. Split out of <see cref="CharacterDataSync"/> when that coordinator
/// reached the architecture line's watchlist — what a mark DOES is the ledger's
/// (<c>LocalFavouriteMarks</c>, Runtime), what carries it is the character record's
/// (<see cref="CharacterDataMsg.FavouriteMarks"/>), and WHEN the two meet is this
/// one place.
/// </summary>
internal sealed class LocalFavouriteMarkSync(IMapper mapper, ICharacterNativeSystem nativeSystem, ILogger log)
{
	private readonly IMapper _mapper = mapper;
	private readonly ICharacterNativeSystem _nativeSystem = nativeSystem;
	private readonly ILogger _log = log;

	/// <summary>
	/// Capture the local character, with the marks it carries. Two things happen
	/// here and they are one rule: a mark whose item is now the local player's OWN
	/// becomes that item's own flag FIRST — so the snapshot this report builds
	/// already carries it (see <see cref="FavouriteMarkAdoption"/>) — and the marks
	/// still standing ride the player's character record, which is the only store
	/// scoped to the world they were made in. Asking here rather than at each event
	/// is what makes the rule complete: every path that can hand this player an item
	/// ends in a report, so a path nobody enumerated still converges.
	/// </summary>
	internal CharacterDataMsg Capture(Body body, out string? nativeFailure)
	{
		FavouriteMarkAdoption.Adopt(body, _log);
		var captured = CharacterDataCapture.Capture(_mapper, body, out nativeFailure, _nativeSystem);
		captured.FavouriteMarks = LocalFavouriteMarkStore.Current.Snapshot();
		return captured;
	}

	/// <summary>
	/// This player's character came back (a reconnect hand-over, a continue, a
	/// respawn): the record's marks REPLACE the live set, never merge into it. The
	/// record is the whole truth for THIS world, and a mark it does not carry belongs
	/// to a world this client has left — the stale key the ruling forbids applying.
	/// </summary>
	internal void SeedFrom(CharacterDataMsg restored)
	{
		LocalFavouriteMarkStore.Current.Seed(restored.FavouriteMarks);
		_log.LogInformation("Character restore brought back {Count} local favourite mark(s).", LocalFavouriteMarkStore.Current.Count);
	}

	/// <summary>The session ended, or a new run started: the marks are world-scoped, so the next world's character record is what seeds the next set.</summary>
	internal void Reset() => LocalFavouriteMarkStore.Current.Reset();
}
