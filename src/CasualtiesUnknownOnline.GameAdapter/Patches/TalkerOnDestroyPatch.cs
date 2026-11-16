using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The native <c>Talker.OnDestroy</c> writes <c>this.text.text = ""</c> while a world is
/// being torn down; when the talker was created without its text widget, <c>text</c> is
/// already gone and Unity throws a <see cref="NullReferenceException"/>. This is teardown
/// cleanup, not gameplay state. Swallow that one exception; every other failure still
/// reaches the caller.
/// </summary>
internal static class TalkerOnDestroyPatch
{
	[HarmonyPatch(typeof(Talker), "OnDestroy")]
	internal static class OnDestroyFinalizer
	{
		private static Exception? Finalizer(Exception? __exception) =>
			__exception is NullReferenceException ? null : __exception;
	}
}
