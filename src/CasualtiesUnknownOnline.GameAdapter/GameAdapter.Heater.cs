using CasualtiesUnknownOnline.Runtime.Session;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The heater-cook domain members of <see cref="GameAdapter"/> (partial split
/// at the 600-line gate): the deep module lives in
/// <see cref="Items.HeaterCookSync"/>; this surface only owns the field and
/// the IPatchBridge forwards the thin Heater patch calls.
/// </summary>
public sealed partial class GameAdapter
{
	private readonly Items.HeaterCookSync _heaterCookSync;

	bool IPatchBridge.IsHeaterCookAuthority =>
		_session.Role != SessionRole.Guest || !_session.SessionActive;

	ulong IPatchBridge.OnHeaterCookBegin(Item item) => _heaterCookSync.OnCookCandidate(item);

	void IPatchBridge.OnHeaterCookCompleted(ulong sourceItemId, Item cookedItem, float sourceCondition, Vector2 sourcePosition) =>
		_heaterCookSync.OnCookCompleted(sourceItemId, cookedItem, sourceCondition, sourcePosition);

	void IPatchBridge.OnHeaterCookCaptureFailed(ulong sourceItemId) => _heaterCookSync.OnCaptureFailed(sourceItemId);
}
