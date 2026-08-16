using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The #87 start-gate alert surface. The adapter is compile-excluded from the
/// test project (it binds game/Unity assemblies), so the pure queue and the
/// patch shape are exercised reflectively — the same host as the other
/// contract tests.
/// </summary>
public class StartGateAlertPatchTests
{
	private static readonly Type Queue = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Run.StartGateAlertQueue",
		throwOnError: true)!;

	private static readonly Type Alert = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Run.StartGateAlert",
		throwOnError: true)!;

	private static readonly Type Patch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.PlayerCameraDoAlertPatch",
		throwOnError: true)!;

	private static object NewQueue() =>
		Activator.CreateInstance(Queue, nonPublic: true)
		?? throw new InvalidOperationException("StartGateAlertQueue could not be constructed.");

	private static bool TryDefer(object queue, string text, bool important)
	{
		var method = Queue.GetMethod("TryDefer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("StartGateAlertQueue.TryDefer not found.");
		return (bool)method.Invoke(queue, [text, important])!;
	}

	private static object[] TakeAll(object queue)
	{
		var method = Queue.GetMethod("TakeAll", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("StartGateAlertQueue.TakeAll not found.");
		return [.. ((IEnumerable)method.Invoke(queue, null)!).Cast<object>()];
	}

	private static bool HasPending(object queue)
	{
		var property = Queue.GetProperty("HasPending", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("StartGateAlertQueue.HasPending not found.");
		return (bool)property.GetValue(queue)!;
	}

	private static (string Text, bool Important) Facts(object alert)
	{
		var text = Alert.GetProperty("Text", BindingFlags.Instance | BindingFlags.Public)?.GetValue(alert) as string
			?? throw new InvalidOperationException("StartGateAlert.Text not found.");
		var important = Alert.GetProperty("Important", BindingFlags.Instance | BindingFlags.Public)?.GetValue(alert) is bool flag
			? flag
			: throw new InvalidOperationException("StartGateAlert.Important not found.");
		return (text, important);
	}

	[Fact]
	public void Queue_PreservesCaptureOrder_AndTakeAllDrains()
	{
		var queue = NewQueue();
		Assert.True(TryDefer(queue, "layertitle", true));
		Assert.True(TryDefer(queue, "delayed description", false));
		Assert.True(HasPending(queue));

		var taken = TakeAll(queue).Select(Facts).ToArray();
		Assert.Equal(2, taken.Length);
		Assert.Equal(("layertitle", true), taken[0]);
		Assert.Equal(("delayed description", false), taken[1]);

		Assert.False(HasPending(queue), "TakeAll must drain the queue");
		Assert.Empty(TakeAll(queue));
	}

	[Fact]
	public void Queue_ClearDropsEveryDeferredAlert()
	{
		var queue = NewQueue();
		TryDefer(queue, "layertitle", true);
		var clear = Queue.GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("StartGateAlertQueue.Clear not found.");
		clear.Invoke(queue, null);

		Assert.False(HasPending(queue));
		Assert.Empty(TakeAll(queue));
	}

	[Fact]
	public void Prefix_SkipsOnlyWhenTheBridgeDefers_AndRunsWithoutASession()
	{
		var prefix = Patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("PlayerCameraDoAlertPatch.Prefix not found.");
		var parameters = prefix.GetParameters();
		Assert.True(prefix.ReturnType == typeof(bool), "the prefix must return the Harmony skip verdict");
		Assert.True(parameters.Length == 2
			&& parameters[0].Name == "text" && parameters[0].ParameterType == typeof(string)
			&& parameters[1].Name == "important" && parameters[1].ParameterType == typeof(bool),
			$"Prefix must have exactly (string text, bool important), got {parameters.Length} parameter(s)");

		// No GameAdapter is constructed in the test process — PatchBridge.Impl is
		// null, the no-session/solo path must let the original DoAlert run.
		Assert.True((bool)prefix.Invoke(null, ["any", false])!);
	}

	[Fact]
	public void PatchInventory_ContainsThePlayerCameraDoAlertContract()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		var contracts = (IEnumerable)build.Invoke(null, null)!;
		var found = contracts.Cast<object>().Any(c =>
		{
			var type = c.GetType();
			var target = type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			var method = type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			return target == "PlayerCamera" && method == "DoAlert";
		});

		Assert.True(found, "PatchInventory must declare the PlayerCamera.DoAlert patch contract (#87).");
	}
}
