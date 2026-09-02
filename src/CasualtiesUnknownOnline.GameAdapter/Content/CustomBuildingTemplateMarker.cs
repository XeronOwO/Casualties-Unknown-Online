using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Marks a GameObject built by <see cref="CustomBuildingTemplateFactory"/> as a
/// CUO custom building template. The marker lets <c>Utils.Create</c> paths know
/// that a clone must be activated (the cached template itself is inactive) and
/// distinguishes mod building templates from vanilla prefabs without exposing
/// the template dictionary to Harmony helpers.
/// </summary>
internal sealed class CustomBuildingTemplateMarker : MonoBehaviour
{
}
