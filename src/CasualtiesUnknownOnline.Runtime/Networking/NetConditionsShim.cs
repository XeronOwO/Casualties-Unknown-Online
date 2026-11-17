using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// The diagnostics network shim behind <see cref="INetConditionsControl"/>: it decides whether an
/// outbound payload is dropped (unreliable only) or delayed, keeps per-peer due times monotonic so delay
/// never invents a reordering that TCP cannot produce, and owns the writer thread that hands each frame
/// back to the transport once its due time arrives. The transport stays the only object that touches a
/// socket; this class only schedules.
/// </summary>
internal sealed class NetConditionsShim(IpDirectTransport transport)
{
	private readonly object _sync = new();
	private readonly List<(long DueTicks, ulong PeerId, byte[] Frame)> _delayed = [];
	private readonly Dictionary<ulong, long> _lastDueTicks = [];
	private readonly Random _roll = new();
	private NetConditionsProfile _profile = NetConditionsProfile.Off;
	private Thread? _thread;
	private volatile bool _running;

	internal NetConditionsProfile Profile
	{
		get
		{
			lock (_sync)
			{
				return _profile;
			}
		}
	}

	/// <summary>
	/// Applies the clamped profile. A latency starts the writer thread; turning shaping off drops
	/// whatever is still queued instead of replaying a stale burst into a later session.
	/// </summary>
	internal void Set(NetConditionsProfile profile)
	{
		var clamped = Clamp(profile);
		lock (_sync)
		{
			_profile = clamped;
			_lastDueTicks.Clear();
			if (!clamped.Enabled)
			{
				_delayed.Clear();
			}
		}

		if (clamped.Enabled && clamped.LatencyMs > 0)
		{
			EnsureThread();
		}
	}

	/// <summary>
	/// True when this payload was handled here (dropped or queued); false when the caller should write
	/// it now.
	/// </summary>
	internal bool Handle(ulong peerId, byte[] frame, bool reliable)
	{
		var profile = Profile;
		if (!profile.Enabled)
		{
			return false;
		}

		if (DropsUnreliable(profile, reliable, NextRoll()))
		{
			transport.LogDroppedPayload(peerId, frame.Length);
			return true;
		}

		var delayMs = DelayMs(profile, NextRoll());
		if (delayMs <= 0)
		{
			return false;
		}

		Schedule(peerId, frame, delayMs);
		return true;
	}

	/// <summary>Stops the writer thread and forgets the profile and any queued frame.</summary>
	internal void Stop()
	{
		_running = false;
		_thread?.Join(500);
		_thread = null;
		lock (_sync)
		{
			_delayed.Clear();
			_lastDueTicks.Clear();
			_profile = NetConditionsProfile.Off;
		}
	}

	internal static NetConditionsProfile Clamp(NetConditionsProfile profile) => profile with
	{
		LatencyMs = ClampInt(profile.LatencyMs, 0, NetConditionsProfile.MaxLatencyMs),
		JitterMs = ClampInt(profile.JitterMs, 0, NetConditionsProfile.MaxJitterMs),
		LossPercent = ClampInt(profile.LossPercent, 0, 100),
	};

	/// <summary>A reliable payload is never dropped; an unreliable one drops below the percentage.</summary>
	internal static bool DropsUnreliable(NetConditionsProfile profile, bool reliable, double roll) =>
		profile.Enabled && !reliable && profile.LossPercent > 0 && roll * 100.0 < profile.LossPercent;

	/// <summary>The extra milliseconds this payload waits: latency, spread by jitter on both sides.</summary>
	internal static int DelayMs(NetConditionsProfile profile, double roll) =>
		profile.Enabled && profile.LatencyMs > 0
			? profile.LatencyMs + (profile.JitterMs > 0 ? (int)Math.Round((roll * 2.0 - 1.0) * profile.JitterMs) : 0)
			: 0;

	private static int ClampInt(int value, int min, int max) => value < min ? min : value > max ? max : value;

	private double NextRoll()
	{
		lock (_sync)
		{
			return _roll.NextDouble();
		}
	}

	private void Schedule(ulong peerId, byte[] frame, int delayMs)
	{
		var due = Stopwatch.GetTimestamp() + (delayMs * Stopwatch.Frequency / 1000);
		lock (_sync)
		{
			if (_lastDueTicks.TryGetValue(peerId, out var last) && due < last)
			{
				due = last;
			}

			_lastDueTicks[peerId] = due;
			_delayed.Add((due, peerId, frame));
		}
	}

	private void EnsureThread()
	{
		if (_thread is not null)
		{
			return;
		}

		_running = true;
		_thread = new Thread(Loop)
		{
			IsBackground = true,
			Name = "CUO IP-direct delay writer",
		};
		_thread.Start();
	}

	private void Loop()
	{
		while (_running)
		{
			List<(ulong PeerId, byte[] Frame)> due = [];
			lock (_sync)
			{
				var now = Stopwatch.GetTimestamp();
				for (var index = _delayed.Count - 1; index >= 0; index--)
				{
					if (_delayed[index].DueTicks <= now)
					{
						due.Add((_delayed[index].PeerId, _delayed[index].Frame));
						_delayed.RemoveAt(index);
					}
				}
			}

			foreach (var (peerId, frame) in due)
			{
				transport.WriteDelayedFrame(peerId, frame);
			}

			Thread.Sleep(due.Count == 0 ? 2 : 0);
		}
	}
}
