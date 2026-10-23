using System;
using System.Collections.Generic;
using System.IO;
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
	private const int ChunkBytes = BulkTransferPolicy.DefaultChunkBytes;
	private const int Window = 16;
	private const double SendIntervalSeconds = 0.04d;
	private const double TransferDeadlineSeconds = 60d;
	private const double StartDelaySeconds = 4d;

	private readonly IModContext _context;
	private readonly MusicDevice _device;
	private readonly HashSet<ulong> _peers = [];
	private readonly Dictionary<int, HashSet<ulong>> _ackedBy = [];

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
			if (member != _localId)
			{
				_peers.Add(member);
			}
		};
		context.PlayerLeft += member => _peers.Remove(member);
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
		if (_guest)
		{
			return "only the host can share a track in this stage (a guest sharer needs the host's cache for late joiners).";
		}

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
		_startTicks = DateTime.UtcNow.AddSeconds(StartDelaySeconds).Ticks;
		_nextSendAt = Time.realtimeSinceStartupAsDouble;
		_deadlineAt = _nextSendAt + TransferDeadlineSeconds;
		_started = false;

		if (!_context.Packets.Broadcast(MusicWire.Announce, MusicWire.AnnounceValue(_transfer, _name, bytes.Length, _sender.File.Sha256, ChunkBytes, _startTicks)))
		{
			Stop();
			return "the announce was refused (no session or no permission).";
		}

		_context.Logger.LogInformation(
			$"[Music] sharing {_name}: {bytes.Length} bytes in {_sender.File.ChunkCount} chunk(s), {wav.Seconds:0.0}s, starting at {new DateTime(_startTicks, DateTimeKind.Utc):HH:mm:ss}Z.");
		return $"sharing {_name} ({wav.Seconds:0.0}s, {_sender.File.ChunkCount} chunks) — playback starts when every member has it.";
	}

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
		_device.Stop();
	}

	/// <summary>Per-frame tick: send the next chunks, start the playback, enforce the deadlines.</summary>
	internal void Tick()
	{
		var now = Time.realtimeSinceStartupAsDouble;
		TryStartPending();
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
			if (!_context.Packets.Broadcast(MusicWire.Chunk, MusicWire.ChunkValue(_transfer, chunk.Index, chunk.Bytes)))
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
		_context.Packets.Broadcast(MusicWire.Play, MusicWire.PlayValue(_transfer, _startTicks));
		_context.Logger.LogInformation($"[Music] {_name} delivered to every member; playback starts at the agreed instant.");
		ArmOrPlay(_file, _name, _startTicks);
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

		_context.Packets.SendToHost(MusicWire.Ack, MusicWire.AckValue(transfer, index));

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
		_context.Logger.LogInformation($"[Music] {_name} verified ({assembled.Length} bytes); waiting for the start instant.");
	}

	internal void OnAck(IModPacketContext context)
	{
		if (_sender is null || !MusicWire.TryReadAck(context.Value, out var transfer, out var index))
		{
			return;
		}

		if (!string.Equals(transfer, _transfer, StringComparison.Ordinal) || _sender.Complete)
		{
			return;
		}

		if (!_ackedBy.TryGetValue(index, out var acked))
		{
			acked = [];
			_ackedBy[index] = acked;
		}

		acked.Add(context.SenderSteamId);
		if (_peers.Count == 0 || acked.Count < _peers.Count)
		{
			return;
		}

		_ackedBy.Remove(index);
		_sender.Acknowledged(index);
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
