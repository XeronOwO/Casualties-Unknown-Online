using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.BulkTransfer.Core;
using CasualtiesUnknownOnline.BulkTransfer.Core.Audio;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.Music;

/// <summary>
/// The sharing half's state machine. The sharer slices the file with <see cref="BulkSender"/>, broadcasts chunks
/// on the mod's declared packets at a fixed interval, and starts playback only when every other member has
/// acknowledged the whole file (the ticket's "transfer first, then play"). A receiver fills a
/// <see cref="BulkReceiver"/>, acks each chunk, and plays the verified bytes at the sharer's announced instant,
/// at the offset that instant implies (so a receiver that completes late starts in the right place rather than at
/// zero).
///
/// <para>
/// Stage A's limits, stated: the SHARER must be the host (a guest's transfer would need the host's copy to cache
/// and re-serve the file for late joiners, which is the next stage); the packet payload is one chunk per fixed
/// interval, so the rate yields to gameplay traffic; and a transfer that does not complete inside its deadline is
/// abandoned with a log line rather than wedging the session.
/// </para>
/// </summary>
internal sealed class MusicShareService
{
	private const int ChunkBytes = BulkTransferPolicy.MaximumChunkBytes;
	private const int Window = 8;

	/// <summary>How many chunks a receiver holds before it reports its contiguous high-water mark again.</summary>
	private const int AckBatch = 4;

	/// <summary>
	/// One chunk per 60 ms ≈ 16.7 frames a second, inside the mod channel's 20 frames per sender per second
	/// (<c>ModRateLimitPolicy.ModMessagesPerSecond</c>): the interval is what keeps the transfer from being
	/// dropped as a burst, and it is what yields the channel to gameplay traffic.
	/// </summary>
	private const double SendIntervalSeconds = 0.06d;
	private const double TransferDeadlineSeconds = 60d;
	private const double StartDelaySeconds = 4d;

	private readonly IModContext _context;
	private readonly MusicDevice _device;
	private readonly HashSet<ulong> _peers = [];
	private readonly Dictionary<int, HashSet<ulong>> _ackedBy = [];

	/// <summary>
	/// The peers known when the share STARTED: a member who joins mid-transfer is served from the cache once the
	/// file is complete, and its silence must not stall the original transfer (the quorum is this set, not the
	/// live roster).
	/// </summary>
	private readonly HashSet<ulong> _sharePeers = [];

	private BulkSender? _sender;
	private byte[]? _file;
	private string _transfer = string.Empty;
	private string _name = string.Empty;
	private long _startTicks;
	private double _nextSendAt;
	private double _deadlineAt;
	private bool _started;
	private byte[]? _pendingBytes;
	private string _pendingName = string.Empty;
	private long _pendingTicks = long.MaxValue;

	private BulkReceiver? _receiver;
	private byte[]? _received;
	private double _receiveDeadlineAt;
	private readonly ulong _localId;
	private bool _guest;

	/// <summary>The host's re-serve cache: the last complete, verified transfer, so a late joiner can catch up.</summary>
	private string _cacheTransfer = string.Empty;
	private string _cacheName = string.Empty;
	private byte[]? _cacheBytes;
	private long _cacheStartTicks = long.MaxValue;
	private int _cacheChunkBytes = ChunkBytes;

	/// <summary>Per late joiner, a fresh sender and the instant its next chunk may leave.</summary>
	private readonly Dictionary<ulong, CatchUp> _catchUp = [];

	/// <summary>
	/// Members who joined while a transfer was still in flight: their catch-up can only start once the file is
	/// cached, so they are served when <see cref="Cache"/> runs rather than losing the track entirely.
	/// </summary>
	private readonly HashSet<ulong> _pendingServe = [];

	internal MusicShareService(IModContext context, MusicDevice device)
	{
		_context = context;
		_device = device;
		_localId = context.Session.LocalSteamId;
		// The session snapshot is BIND time, before any session exists: the live role is learned from the event
		// the host never fires (SessionActivated fires on a guest's first handshake).
		context.SessionActivated += () => _guest = true;
		foreach (var member in context.Session.MemberSteamIds)
		{
			_peers.Add(member);
		}

		context.PlayerJoined += member =>
		{
			if (member == _localId)
			{
				return;
			}

			_peers.Add(member);
			ServeLateJoiner(member);
		};
		context.PlayerLeft += member =>
		{
			_peers.Remove(member);
			_catchUp.Remove(member);
		};
	}

	/// <summary>True while this client shares a track.</summary>
	internal bool Sharing => _sender is not null;

	/// <summary>True while this client receives one.</summary>
	internal bool Receiving => _receiver is not null;

	/// <summary>The progress line the console's status verb prints.</summary>
	internal string Status()
	{
		var playback = _device.Playing ? $"playing ({_device.Position:0.0}s of {_device.Length:0.0}s)" : "not playing";
		if (_sender is not null)
		{
			var acked = _sender.File.ChunkCount - _sender.Missing().Count;
			return $"sharing {_name}: {acked}/{_sender.File.ChunkCount} chunk(s) acknowledged by everyone; {playback}.";
		}

		if (_receiver is not null)
		{
			return _received is not null
				? $"received {_name}: {_receiver.File.ChunkCount}/{_receiver.File.ChunkCount} chunk(s), verified; {playback}."
				: $"receiving {_name}: {_receiver.File.ChunkCount - _receiver.MissingCount}/{_receiver.File.ChunkCount} chunk(s); {playback}.";
		}

		return playback + ".";
	}

	/// <summary>Start sharing a WAV file (host only in this stage).</summary>
	internal string Share(string path)
	{
		if (!File.Exists(path))
		{
			return $"no file at {path}";
		}

		byte[] bytes;
		try
		{
			bytes = File.ReadAllBytes(path);
		}
		catch (Exception exception)
		{
			return $"the file could not be read: {exception.GetType().Name}: {exception.Message}";
		}

		var wav = WavFile.Decode(bytes);
		if (!wav.Decoded)
		{
			return $"not playable: {wav.Refusal}";
		}

		Stop();
		_transfer = MusicWire.NewTransferId();
		_name = Path.GetFileName(path);
		_file = bytes;
		_sender = new BulkSender(_transfer, bytes, ChunkBytes);
		_ackedBy.Clear();
		_sharePeers.Clear();
		foreach (var peer in _peers)
		{
			_sharePeers.Add(peer);
		}

		_startTicks = DateTime.UtcNow.AddSeconds(StartDelaySeconds).Ticks;
		_nextSendAt = Time.realtimeSinceStartupAsDouble;
		_deadlineAt = _nextSendAt + TransferDeadlineSeconds;
		_started = false;

		if (!Send(MusicWire.Announce, MusicWire.AnnounceValue(_transfer, _name, bytes.Length, _sender.File.Sha256, ChunkBytes, _startTicks)))
		{
			Stop();
			return "the announce was refused (no session or no permission).";
		}

		_context.Logger.LogInformation(
			$"[Music] sharing {_name}: {bytes.Length} bytes in {_sender.File.ChunkCount} chunk(s), {wav.Seconds:0.0}s, starting at {new DateTime(_startTicks, DateTimeKind.Utc):HH:mm:ss}Z.");
		return $"sharing {_name} ({wav.Seconds:0.0}s, {_sender.File.ChunkCount} chunks) — playback starts when every member has it.";
	}

	/// <summary>
	/// Send a share packet as whoever this client is: the host broadcasts, a guest reports to the host, whose copy
	/// runs the chain and relays it to the other members (delivery <c>EveryOtherMember</c>).
	/// </summary>
	private bool Send(string packetId, ModValue value) =>
		_guest
			? _context.Packets.SendToHost(packetId, value)
			: _context.Packets.Broadcast(packetId, value);

	/// <summary>Stop sharing or receiving and stop the local playback.</summary>
	internal void Stop()
	{
		if (_sender is not null)
		{
			_context.Logger.LogInformation($"[Music] sharing {_name} stopped.");
		}

		_sender = null;
		_receiver = null;
		_file = null;
		_received = null;
		_ackedBy.Clear();
		_sharePeers.Clear();
		_catchUp.Clear();
		_pendingServe.Clear();
		_device.Stop();
	}

	/// <summary>
	/// Host only: a member joined while a track is known — start a private catch-up transfer to that member, so a
	/// late joiner hears the SAME track at the offset the group is at rather than nothing or the wrong place.
	/// </summary>
	private void ServeLateJoiner(ulong member)
	{
		if (_guest || _sharePeers.Contains(member))
		{
			return;
		}

		if (_cacheBytes is null || _cacheStartTicks == long.MaxValue)
		{
			// Only a member who joined WHILE a share is moving has anything to be served later; a member who was
			// already there when the share began is one of its acks and must not be queued (a second catch-up
			// transfer for it would spend the channel on a track it already holds).
			if (_sender is not null)
			{
				_pendingServe.Add(member);
			}

			return;
		}

		var sender = new BulkSender(_cacheTransfer, _cacheBytes, _cacheChunkBytes);
		if (!_context.Packets.SendToPeer(member, MusicWire.Announce,
				MusicWire.AnnounceValue(_cacheTransfer, _cacheName, _cacheBytes.Length, sender.File.Sha256, _cacheChunkBytes, _cacheStartTicks)))
		{
			_context.Logger.LogWarning($"[Music] a late joiner could not be sent the announce of {_cacheName}.");
			return;
		}

		_catchUp[member] = new CatchUp(sender, Time.realtimeSinceStartupAsDouble);
		_context.Logger.LogInformation($"[Music] serving {_cacheName} to late joiner {member}.");
	}

	/// <summary>Send one chunk per interval to each late joiner still catching up.</summary>
	private void TickCatchUp(double now)
	{
		foreach (var member in _catchUp.Keys.ToArray())
		{
			var catchUp = _catchUp[member];
			if (now < catchUp.NextSendAt)
			{
				continue;
			}

			catchUp.NextSendAt = now + SendIntervalSeconds;
			if (catchUp.Sender.Next(Window) is { } chunk)
			{
				_context.Packets.SendToPeer(member, MusicWire.Chunk, MusicWire.ChunkValue(_cacheTransfer, chunk.Index, chunk.Bytes));
				continue;
			}

			if (!catchUp.Sender.Complete)
			{
				continue;
			}

			_context.Packets.SendToPeer(member, MusicWire.Play, MusicWire.PlayValue(_cacheTransfer, _cacheStartTicks));
			_catchUp.Remove(member);
			_context.Logger.LogInformation($"[Music] late joiner {member} received {_cacheName}; the start instant was sent.");
		}
	}

	/// <summary>Per-frame tick: send the next chunks, start the playback, enforce the deadlines.</summary>
	internal void Tick()
	{
		var now = Time.realtimeSinceStartupAsDouble;
		TryStartPending();
		TickCatchUp(now);
		if (_sender is not null)
		{
			TickSender(now);
		}

		if (_receiver is not null && _received is null && now >= _receiveDeadlineAt)
		{
			_context.Logger.LogWarning($"[Music] receiving {_name} abandoned: no complete file inside the deadline.");
			_receiver = null;
		}
	}

	private void TickSender(double now)
	{
		if (_sender is null)
		{
			return;
		}

		if (!_started && now >= _deadlineAt)
		{
			_context.Logger.LogWarning($"[Music] sharing {_name} abandoned: its peers did not acknowledge the whole file inside the deadline.");
			Stop();
			return;
		}

		if (!_started && now >= _nextSendAt && _sender.Next(Window) is { } chunk)
		{
			if (!Send(MusicWire.Chunk, MusicWire.ChunkValue(_transfer, chunk.Index, chunk.Bytes)))
			{
				_context.Logger.LogWarning($"[Music] a chunk of {_name} was refused by the packet channel; the transfer will retry it.");
			}

			_nextSendAt = now + SendIntervalSeconds;
		}

		if (_started || !_sender.Complete)
		{
			return;
		}

		_started = true;
		Send(MusicWire.Play, MusicWire.PlayValue(_transfer, _startTicks));
		_context.Logger.LogInformation($"[Music] {_name} delivered to every member; playback starts at the agreed instant.");
		Cache(_transfer, _name, _file, _startTicks, ChunkBytes);
		ArmOrPlay(_file, _name, _startTicks);
	}

	/// <summary>Remember the last complete transfer so a late joiner can be served (the host is the only server).</summary>
	private void Cache(string transfer, string name, byte[]? bytes, long startTicks, int chunkBytes)
	{
		if (bytes is null || _guest)
		{
			return;
		}

		_cacheTransfer = transfer;
		_cacheName = name;
		_cacheBytes = bytes;
		_cacheStartTicks = startTicks;
		_cacheChunkBytes = chunkBytes;

		// A member who joined mid-transfer was remembered; now that the file exists, serve it.
		foreach (var member in _pendingServe.ToArray())
		{
			ServeLateJoiner(member);
		}

		_pendingServe.Clear();
	}

	/// <summary>One late joiner's private catch-up transfer.</summary>
	private sealed class CatchUp(BulkSender sender, double nextSendAt)
	{
		internal BulkSender Sender { get; } = sender;

		internal double NextSendAt { get; set; } = nextSendAt;
	}

	/// <summary>
	/// Start the track at the agreed instant: before it arrives the clip is armed and started by the tick, after
	/// it the clip starts at the offset the group is already at — one rule for the sharer and every receiver.
	/// </summary>
	private void ArmOrPlay(byte[]? bytes, string name, long startTicks)
	{
		if (bytes is null)
		{
			return;
		}

		var start = new DateTime(startTicks, DateTimeKind.Utc);
		var late = DateTime.UtcNow - start;
		if (late < TimeSpan.Zero)
		{
			_pendingBytes = bytes;
			_pendingName = name;
			_pendingTicks = startTicks;
			_context.Logger.LogInformation($"[Music] {name} armed; it starts in {-late.TotalSeconds:0.0}s at the agreed instant.");
			return;
		}

		var refused = _device.PlayBytes(name, bytes, offsetSeconds: late.TotalSeconds);
		if (refused is not null)
		{
			_context.Logger.LogWarning($"[Music] {name} did not play: {refused}");
		}
	}

	/// <summary>Start an armed track when the agreed instant arrives.</summary>
	private void TryStartPending()
	{
		if (_pendingBytes is null || DateTime.UtcNow.Ticks < _pendingTicks)
		{
			return;
		}

		var bytes = _pendingBytes;
		var name = _pendingName;
		_pendingBytes = null;
		_pendingTicks = long.MaxValue;
		var refused = _device.PlayBytes(name, bytes);
		if (refused is not null)
		{
			_context.Logger.LogWarning($"[Music] {name} did not play: {refused}");
		}
	}

	// ---- packet chains ----

	internal void OnAnnounce(IModPacketContext context)
	{
		if (!MusicWire.TryReadAnnounce(context.Value, out var transfer, out var name, out var length, out var sha256, out var chunkBytes, out var startTicks))
		{
			return;
		}

		if (_sender is not null)
		{
			return; // our own announce relayed back
		}

		var file = new BulkFile(transfer, length, sha256, chunkBytes);
		_receiver = new BulkReceiver(file);
		_received = null;
		_name = name;
		_startTicks = startTicks;
		_receiveDeadlineAt = Time.realtimeSinceStartupAsDouble + TransferDeadlineSeconds;
		_context.Logger.LogInformation($"[Music] receiving {name}: {file.ChunkCount} chunk(s) from {context.SenderSteamId}.");
	}

	internal void OnChunk(IModPacketContext context)
	{
		if (_receiver is null || !MusicWire.TryReadChunk(context.Value, out var transfer, out var index, out var bytes))
		{
			return;
		}

		if (!string.Equals(transfer, _receiver.File.Id, StringComparison.Ordinal))
		{
			return;
		}

		if (_receiver.Take(new BulkChunk(index, bytes)) is BulkChunkVerdict.Malformed)
		{
			_context.Logger.LogWarning($"[Music] refused a malformed chunk {index} of {_name}.");
			return;
		}

		// Acks are BATCHED: the receiver reports its contiguous high-water mark every 8 chunks and once at the
		// end, because one frame per chunk would double the host's traffic (its relay of every ack) and the mod
		// channel's 20 frames per sender per second would drop the transfer into retransmits.
		var prefix = _receiver.ContiguousPrefix();
		if (prefix > 0 && (prefix == _receiver.File.ChunkCount || prefix % AckBatch == 0))
		{
			// The ack goes the same way a share's packets do: a guest reports to the host, the host (which may
			// itself be the receiver when a GUEST shares) broadcasts it, so the sharer's window advances in both
			// directions. SendToHost on the host would run the chain locally and the guest sharer would never
			// hear it.
			Send(MusicWire.Ack, MusicWire.AckValue(transfer, prefix - 1));
		}

		if (!_receiver.Complete)
		{
			return;
		}

		var assembled = _receiver.TryAssemble();
		if (assembled is null)
		{
			_context.Logger.LogWarning($"[Music] {_name} arrived but its hash does not match the announced one; it will not play.");
			_receiver = null;
			return;
		}

		_received = assembled;
		Cache(_receiver.File.Id, _name, assembled, _startTicks, _receiver.File.ChunkBytes);
		_context.Logger.LogInformation($"[Music] {_name} verified ({assembled.Length} bytes); waiting for the start instant.");
	}

	internal void OnAck(IModPacketContext context)
	{
		if (!MusicWire.TryReadAck(context.Value, out var transfer, out var highWater))
		{
			return;
		}

		// An ack names the receiver's contiguous high-water mark: every chunk up to it is held by that member.
		if (_catchUp.TryGetValue(context.SenderSteamId, out var catchUp)
			&& string.Equals(transfer, _cacheTransfer, StringComparison.Ordinal))
		{
			for (var i = 0; i <= highWater; i++)
			{
				catchUp.Sender.Acknowledged(i);
			}

			return;
		}

		if (_sender is null || !string.Equals(transfer, _transfer, StringComparison.Ordinal) || _sender.Complete)
		{
			return;
		}

		if (!_sharePeers.Contains(context.SenderSteamId))
		{
			// A member who joined mid-transfer is served from the cache afterwards; its silence must not stall
			// the original transfer's quorum.
			return;
		}

		for (var index = 0; index <= highWater; index++)
		{
			if (!_ackedBy.TryGetValue(index, out var acked))
			{
				acked = [];
				_ackedBy[index] = acked;
			}

			acked.Add(context.SenderSteamId);
			if (_sharePeers.Count == 0 || acked.Count < _sharePeers.Count)
			{
				continue;
			}

			_ackedBy.Remove(index);
			_sender.Acknowledged(index);
		}
	}

	internal void OnPlay(IModPacketContext context)
	{
		if (_sender is not null || _receiver is null || !MusicWire.TryReadPlay(context.Value, out var transfer, out _))
		{
			return;
		}

		if (!string.Equals(transfer, _receiver.File.Id, StringComparison.Ordinal))
		{
			return;
		}

		if (_received is null)
		{
			_context.Logger.LogWarning($"[Music] the start of {_name} arrived before the file did; this client stays silent.");
			return;
		}

		_context.Logger.LogInformation($"[Music] playing {_name} at the agreed instant.");
		ArmOrPlay(_received, _name, _startTicks);
	}
}
