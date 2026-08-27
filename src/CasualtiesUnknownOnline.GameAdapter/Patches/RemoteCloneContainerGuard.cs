using CasualtiesUnknownOnline.GameAdapter.Character;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// True when a container/container-parent is a remote clone display proxy — its
/// children are presentation-only and must never enter the item-domain report
/// chain.
/// </summary>
internal static class RemoteCloneContainerGuard
{
	internal static bool IsDisplayProxy(Component component) =>
		component != null && component.GetComponentInParent<RemoteCloneRender>() != null; // Unity object — ==
}
