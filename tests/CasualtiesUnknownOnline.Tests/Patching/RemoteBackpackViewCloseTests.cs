using System;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Regression test for the local backpack instant-close report. The remote
/// backpack focus used to call <c>RemoteBackpackView.Close()</c> from every
/// CUO update and from <c>InvButtonBodyPatch</c> whenever no remote focus was
/// active. That close unconditionally wrote
/// <c>PlayerCamera.radialOpen = false</c>, so a normal local Tab press opened
/// the native radial inventory and then immediately closed it (either in the
/// same CUO update or on the first inventory button render).
/// </summary>
public class RemoteBackpackViewCloseTests
{
	[Fact]
	public void Close_WithoutRemoteFocus_DoesNotCloseTheNativeLocalRadial()
	{
		var viewType = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.RemoteBackpackView",
			throwOnError: true)!;
		var cameraType = GameAssemblyHost.Game.GetType("PlayerCamera", throwOnError: true)!;

		var camera = FormatterServices.GetUninitializedObject(cameraType);
		var mainField = cameraType.GetField("main", BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException("PlayerCamera.main not found.");
		var radialField = cameraType.GetField("radialOpen", BindingFlags.Public | BindingFlags.Instance)
			?? throw new InvalidOperationException("PlayerCamera.radialOpen not found.");
		var focusField = viewType.GetField("_focusedBody", BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException("RemoteBackpackView._focusedBody not found.");
		var close = viewType.GetMethod("Close", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBackpackView.Close not found.");

		focusField.SetValue(null, null);
		SetUnityNativePointerNonZero(camera);
		mainField.SetValue(null, camera);
		radialField.SetValue(camera, true);

		try
		{
			close.Invoke(null, null);

			Assert.True((bool)radialField.GetValue(camera)!,
				"Close with no remote focus must not touch the native radial-open state, otherwise a normal local Tab press is immediately swallowed.");
		}
		finally
		{
			mainField.SetValue(null, null);
		}
	}

	/// <summary>
	/// Unity's overloaded <c>== null</c> treats a zero <c>m_CachedPtr</c> as a
	/// destroyed object even when the managed reference is non-null. The old
	/// Close implementation only wrote <c>radialOpen</c> inside the
	/// <c>PlayerCamera.main != null</c> branch, so this test must make the raw
	/// camera look alive to observe the regression.
	/// </summary>
	private static void SetUnityNativePointerNonZero(object camera)
	{
		for (var type = camera.GetType(); type != null; type = type.BaseType)
		{
			var field = type.GetField("m_CachedPtr", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (field != null)
			{
				field.SetValue(camera, new IntPtr(1));
				return;
			}
		}

		throw new InvalidOperationException("UnityEngine.Object.m_CachedPtr not found on PlayerCamera.");
	}
}
