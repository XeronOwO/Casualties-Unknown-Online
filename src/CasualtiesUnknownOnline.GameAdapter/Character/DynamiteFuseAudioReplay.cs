using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// One-shot audio replay marker for a remote clone's lit dynamite fuse.
/// <see cref="RemoteItemPresentation"/> adds it when the wire state says the
/// owner's fuse is lit, so the clone plays the native fuse AudioSource exactly
/// once (the native use action calls Play on the owner side; the clone must not
/// wait for the explosion to make the fuse audible). The marker persists on the
/// clone item for the rest of the fuse lifetime, so repeated snapshot refreshes
/// never re-trigger the audio.
/// </summary>
internal sealed class DynamiteFuseAudioReplay : MonoBehaviour
{
	private bool _played;

	private void Start()
	{
		if (_played)
		{
			return;
		}

		_played = true;
		var audio = GetComponent<AudioSource>();
		if (audio != null && !audio.isPlaying) // Unity object — ==
		{
			audio.Play();
		}
	}
}
