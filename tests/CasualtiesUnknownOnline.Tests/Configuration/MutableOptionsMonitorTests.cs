using CasualtiesUnknownOnline.Runtime.Configuration;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Configuration;

/// <summary>
/// The default in-memory options monitor: value changes notify listeners and
/// listener disposal stops the notifications (the same contract the BepInEx
/// bridge must satisfy).
/// </summary>
public class MutableOptionsMonitorTests
{
	[Fact]
	public void Set_UpdatesCurrentValueAndNotifiesListeners()
	{
		var monitor = new MutableOptionsMonitor<StateStreamOptions>(new StateStreamOptions());
		var changes = 0;
		using var _ = monitor.OnChange((value, name) =>
		{
			changes++;
			Assert.Equal(5, value.StateStreamHz);
		});

		monitor.Set(new StateStreamOptions { StateStreamHz = 5 });

		Assert.Equal(5, monitor.CurrentValue.StateStreamHz);
		Assert.Equal(1, changes);
	}

	[Fact]
	public void DisposedListener_StopsReceivingChanges()
	{
		var monitor = new MutableOptionsMonitor<StateStreamOptions>(new StateStreamOptions());
		var changes = 0;
		var subscription = monitor.OnChange((_, _) => changes++);

		subscription.Dispose();
		monitor.Set(new StateStreamOptions { StateStreamHz = 10 });

		Assert.Equal(0, changes);
	}
}
