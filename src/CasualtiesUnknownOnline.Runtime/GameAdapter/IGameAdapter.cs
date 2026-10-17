using System;

using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The Game Adapter boundary (architecture.md §4): the only layer that knows
/// the game's private types. One implementation per game build; the Runtime
/// defines the contracts, the adapter project (CUO.GameAdapter) implements them.
///
/// This interface DECLARES NO MEMBER OF ITS OWN — it is the composition of the
/// capability ports, and every consumer resolves the port whose capability it
/// actually uses (<see cref="IGameIntegrationLifecycle"/>,
/// <see cref="IAdapterCapabilityQuery"/>, <see cref="IWorldPresenceQuery"/>,
/// <see cref="IStartGateState"/>, <see cref="IDragUseCueState"/>, <see cref="ILocalHealItemQuery"/>,
/// <see cref="ITraderRecruitRequest"/>, <see cref="INativeInputBlocker"/>,
/// <see cref="IRemoteInventoryPresentation"/>,
/// <see cref="IRemoteMedicalPresentation"/>,
/// <see cref="IPlayerAnchorQuery"/>, <see cref="IJoinFlowPresentation"/>,
/// <see cref="ICarryPresentationPump"/>, <see cref="IOnlineUiNativeFactsQuery"/>,
/// <see cref="IOnlineUiSurface"/>, <see cref="ISpectatorControl"/>).
/// A version adapter therefore implements per capability, and a test double
/// implements only the capability it stands in for.
///
/// <para>
/// The console <c>/give</c> verb's creation seam (<see cref="IItemGrantSource"/>) is
/// deliberately NOT part of this composition: the interaction service consumes it and
/// the adapter depends on that service, so a port here would close a constructor cycle
/// (the batch 20261017-q startup failure). It is a standalone adapter seam instead.
/// </para>
///
/// The composition is the seam's single identity — one resolve that refers to
/// the whole adapter object — and it is FROZEN: adding a member here fails
/// <c>AdapterCapabilityPortShapeTests</c>. A new capability is a new port plus
/// its entry in this list, never a widening of the aggregate.
/// </summary>
public interface IGameAdapter :
	IGameIntegrationLifecycle,
	IAdapterCapabilityQuery,
	IWorldPresenceQuery,
	IStartGateState,
	IDragUseCueState,
	ILocalHealItemQuery,
	ITraderRecruitRequest,
	INativeInputBlocker,
	IRemoteInventoryPresentation,
	IRemoteMedicalPresentation,
	ISpectatorControl,
	IPlayerAnchorQuery,
	IJoinFlowPresentation,
	ICarryPresentationPump,
	IOnlineUiNativeFactsQuery,
	IOnlineUiSurface,
	IDisposable
{
}
