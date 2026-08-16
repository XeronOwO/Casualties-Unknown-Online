using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Tutorial;

/// <summary>
/// Marker on every object the tutorial claw creates (TutorialHandler.Update's
/// objectToCreate branch, TutorialHandler.cs:255-271). The tutorial courses
/// run per side, so the claw props are per-player course objects, NOT shared
/// world items/entities: both sides create their own copy at their own claw
/// position, and letting both copies enter the shared item/entity domains made
/// every prop appear twice on both sides (the claw double-give). A marked item
/// stays id-less until a player actually picks it up — the same flow as a
/// generation-time item (PickupSync.OnPickedUp's id-less branch reports
/// spawn-then-pickup and the domain takes it from there). A marked
/// BuildingEntity stays local-only and never rides EntitySpawned. The marker is
/// attached in the same Utils.Create postfix, before Item.Start/BuildingEntity.
/// Start run (their hooks see it and skip the domain entry).
/// </summary>
internal sealed class TutorialClawProp : MonoBehaviour
{
}
