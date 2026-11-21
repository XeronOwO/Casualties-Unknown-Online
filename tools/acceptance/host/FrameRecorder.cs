using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// Records the clients' own windows while a batch runs, so a row that is judged by what the player
/// sees has frames to read afterwards instead of a description written from the log. The recorder
/// never activates a window (<see cref="WindowCaptureCommand.TryCapture"/> renders it in place) and a
/// failed capture is skipped: the picture is evidence, not a gate on the run.
/// </summary>
internal sealed class FrameRecorder : IDisposable
{
	private readonly string _directory;
	private readonly string _pidsPath;
	private readonly int _intervalMs;
	private readonly CancellationTokenSource _stop = new();
	private readonly Task _loop;
	private int _round;

	public FrameRecorder(string directory, string pidsPath, int intervalMs)
	{
		_directory = directory;
		_pidsPath = pidsPath;
		_intervalMs = Math.Max(150, intervalMs);
		Directory.CreateDirectory(_directory);
		_loop = Task.Run(LoopAsync);
	}

	public int Captured => Volatile.Read(ref _captured);

	private int _captured;

	public void Dispose()
	{
		_stop.Cancel();
		try
		{
			_loop.Wait(TimeSpan.FromSeconds(5));
		}
		catch (AggregateException)
		{
			// The loop only stops on cancellation; a capture that threw ends the round, not the run.
		}

		_stop.Dispose();
	}

	private async Task LoopAsync()
	{
		while (!_stop.IsCancellationRequested)
		{
			try
			{
				CaptureRound();
			}
			catch (IOException)
			{
				// pids.json is rewritten by a restart while the recorder reads it: skip this round.
			}
			catch (JsonException)
			{
			}

			try
			{
				await Task.Delay(_intervalMs, _stop.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				return;
			}
		}
	}

	private void CaptureRound()
	{
		if (!File.Exists(_pidsPath))
		{
			return;
		}

		using var document = JsonDocument.Parse(File.ReadAllText(_pidsPath));
		var round = Interlocked.Increment(ref _round);
		foreach (var role in new[] { "host", "guest", "alt" })
		{
			if (!document.RootElement.TryGetProperty(role, out var client) || client.ValueKind != JsonValueKind.Object)
			{
				continue;
			}

			foreach (var pid in Pids(client))
			{
				var path = Path.Combine(_directory, $"{role}-{round:D3}.png");
				if (WindowCaptureCommand.TryCapture(pid, path, out _))
				{
					Interlocked.Increment(ref _captured);
					break;
				}
			}
		}
	}

	private static IEnumerable<int> Pids(JsonElement client)
	{
		if (client.TryGetProperty("pids", out var many) && many.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in many.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var manyPid) && manyPid > 0)
				{
					yield return manyPid;
				}
			}
		}

		if (client.TryGetProperty("pid", out var single) && single.ValueKind == JsonValueKind.Number && single.TryGetInt32(out var one) && one > 0)
		{
			yield return one;
		}
	}
}
