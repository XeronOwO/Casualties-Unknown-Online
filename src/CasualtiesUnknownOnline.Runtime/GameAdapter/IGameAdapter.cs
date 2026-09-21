using System;

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
/// <see cref="IStartGateState"/>, <see cref="ILocalHealItemQuery"/>,
/// <see cref="ITraderRecruitRequest"/>, <see cref="INativeInputBlocker"/>,
/// <see cref="IRemoteInventoryPresentation"/>,
/// <see cref="IRemoteMedicalPresentation"/>, <see cref="IPlayerAnchorQuery"/>).
/// A version adapter therefore implements per capability, and a test double
/// implements only the capability it stands in for.
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
	ILocalHealItemQuery,
	ITraderRecruitRequest,
	INativeInputBlocker,
	IRemoteInventoryPresentation,
	IRemoteMedicalPresentation,
	IPlayerAnchorQuery,
	IDisposable
{
}
