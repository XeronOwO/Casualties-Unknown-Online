using CasualtiesUnknownOnline.GameAdapter.Character;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The carriage half of the patch bridge (<see cref="ICarriagePatchBridge"/>):
/// the carriage domain's live reads, kept out of <see cref="GameAdapterBridge"/>
/// so that class stays under the architecture line gate while the carriage
/// surface has one focused seam.
/// </summary>
internal sealed class CarriagePatchBridge(GameAdapterDomains domains) : ICarriagePatchBridge
{
	public float GetCarriedEncumbrance(Body body)
	{
		var local = domains.Run.LocalBody;
		if (local == null || local != body) // Unity objects — ==
		{
			return 0f;
		}

		if (!domains.PlayerInteraction.TryGetCarried(domains.Session.LocalSteamId, out var carried))
		{
			return 0f;
		}

		if (!domains.CharacterDataSync.CloneData.TryGetValue(carried, out var data))
		{
			domains.Log.LogDebug("[CarryWeight] no character snapshot for carried {Carried} — no weight added.", carried);
			return 0f;
		}

		var full = CarriedEncumbranceCalculator.ComputeFullEncumbrance(data);
		var contribution = CarriedEncumbranceCalculator.ApplyMultiplier(full, domains.HostRules.PiggybackWeightMultiplier);
		domains.Log.LogDebug("[CarryWeight] carrier {Carrier} gains {Contribution:F2} from {Carried} (full {Full:F2}).",
			domains.Session.LocalSteamId, contribution, carried, full);
		return contribution;
	}

	public bool IsLocalCarrier(Body body)
	{
		var local = domains.Run.LocalBody;
		return local != null
			&& local == body // Unity objects — ==
			&& domains.PlayerInteraction.TryGetCarried(domains.Session.LocalSteamId, out _);
	}

	public void OnLocalCarrierBodyUpdated()
	{
		var local = domains.Run.LocalBody;
		if (local == null) // Unity object — ==
		{
			return;
		}

		if (!domains.PlayerInteraction.TryGetCarried(domains.Session.LocalSteamId, out _))
		{
			return;
		}

		domains.Renderer.RefreshLocalCarrierAttach(local);
	}
}
