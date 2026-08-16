using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Marks a render object the limb-presentation renderer owns (the replicated
/// broken-bone sprite). Separate from <see cref="RemoteCloneRender"/> so the
/// inventory renderer's worn-item cleanup never destroys the limb's wound
/// visuals — both renderers share limb transforms.
/// </summary>
internal sealed class RemoteCloneLimbRender : MonoBehaviour
{
}
