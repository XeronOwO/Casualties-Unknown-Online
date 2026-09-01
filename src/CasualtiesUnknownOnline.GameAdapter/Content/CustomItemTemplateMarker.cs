using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// Marks a GameObject built by <see cref="CustomItemTemplateFactory"/> as a
/// CUO custom item template. The marker lets resource/instantiate helpers know
/// that a clone must be activated (the cached template itself is inactive) and
/// distinguishes mod templates from vanilla prefabs without exposing the
/// template dictionary to IL helpers.
/// </summary>
internal sealed class CustomItemTemplateMarker : MonoBehaviour
{
}
