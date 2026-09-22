using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// Guest side of the checkpoint path. It validates every arriving chunk against
/// the run identity the host announced with its enter-the-world instruction,
/// buffers ONE set keyed by that set's own <c>(RunEpoch, GlobalRevision,
/// ChunkCount)</c>, and restores the kernel once the whole set has arrived.
///
/// The identity rules are the point of this type, so they live here rather than
/// beside the send/receive plumbing: a set that belongs to another run must not
/// reach the buffer at all (a straggler would otherwise hand this side a run its
/// own live streams no longer match), and a chunk of any OTHER set must never
/// occupy a slot the pending set still needs — kept, a leftover index satisfies
/// the count check for a slot the live set never fills and blocks every later
/// set forever.
/// </summary>
internal sealed class GuestCheckpointReceiver(IKernelBatchApplication authority, IKernelWireCodec codec, ILogger log)
{
	private readonly IKernelBatchApplication _authority = authority;
	private readonly IKernelWireCodec _codec = codec;
	private readonly ILogger _log = log;
	private readonly Dictionary<int, WireCheckpoint> _chunks = [];
	private readonly HashSet<ulong> _staleEpochWarned = [];
	private (ulong Epoch, ulong Revision, int ChunkCount)? _pendingIdentity;
	private ulong _expectedHostRunEpoch;

	/// <summary>
	/// Guest: the host announced the run identity this member must serve. Zero
	/// means "no announcement" — the identity already held stays in force.
	/// </summary>
	public void AdoptHostRunEpoch(ulong runEpoch)
	{
		if (runEpoch == 0)
		{
			// A current peer always stamps this (the handshake refuses a mixed pair),
			// so an unstamped instruction is a sender-side defect rather than a value
			// to record.
			_log.LogWarning("World-join instruction carried no run epoch; checkpoint sets stay validated against run {Epoch}.", _expectedHostRunEpoch);
			return;
		}

		var previous = _expectedHostRunEpoch;
		_expectedHostRunEpoch = runEpoch;
		if (previous == 0)
		{
			_log.LogInformation("Host announced run {Epoch}; checkpoint sets are validated against it from here.", runEpoch);
		}
		else
		{
			_log.LogInformation("Host announced run {Epoch} (this side served {Previous}); a set from the previous run is now refused.", runEpoch, previous);
		}
	}

	/// <summary>
	/// Guest: one checkpoint chunk. A set from another run is refused before a
	/// chunk is buffered; when nothing was announced yet (a reconnect's entry
	/// group precedes the join instruction) the first set to restore defines the
	/// identity and every later set is compared against it.
	/// </summary>
	public void HandleChunk(ulong sender, CheckpointEnvelope envelope)
	{
		var chunk = envelope.Checkpoint;

		// The header and the payload are stamped from the same checkpoint at send
		// time, so a frame whose two stamps disagree is malformed by construction.
		if (envelope.Header.RunEpoch != chunk.RunEpoch)
		{
			_log.LogWarning("Checkpoint chunk {Index} from {Sender} carries run {ChunkEpoch} but its header says {HeaderEpoch} — dropped.",
				chunk.ChunkIndex, sender, chunk.RunEpoch, envelope.Header.RunEpoch);
			return;
		}

		if (_expectedHostRunEpoch != 0 && chunk.RunEpoch != _expectedHostRunEpoch)
		{
			// Warn once per offending run: the 60 s repair would otherwise repeat this
			// for as long as a stale sender keeps sending.
			if (_staleEpochWarned.Add(chunk.RunEpoch))
			{
				_log.LogWarning("Dropped checkpoint chunk set from run {Epoch} ({Sender}): this side serves run {Expected}; further drops for that run log at debug.",
					chunk.RunEpoch, sender, _expectedHostRunEpoch);
			}
			else
			{
				_log.LogDebug("Dropped checkpoint chunk {Index} from run {Epoch} ({Sender}); this side serves run {Expected}.",
					chunk.ChunkIndex, chunk.RunEpoch, sender, _expectedHostRunEpoch);
			}

			return;
		}

		var identity = (chunk.RunEpoch, chunk.GlobalRevision, chunk.ChunkCount);
		if (_pendingIdentity is { } pending && pending != identity)
		{
			// The buffered set was superseded before it completed (a newer checkpoint of
			// the same run, the next entry's set, or a set that declares another chunk
			// count): its chunks go as a unit. Kept, a leftover index would satisfy the
			// count check for a slot the live set has not filled, and the assembled set
			// would mix two revisions.
			_log.LogDebug("Checkpoint set run {Epoch} revision {Revision} ({Count} chunks) was superseded by run {NewEpoch} revision {NewRevision} ({NewCount} chunks) after {Buffered} chunk(s); the partial set is dropped.",
				pending.Epoch, pending.Revision, pending.ChunkCount, identity.RunEpoch, identity.GlobalRevision, identity.ChunkCount, _chunks.Count);
			_chunks.Clear();
		}

		_pendingIdentity = identity;
		_chunks[chunk.ChunkIndex] = chunk;
		if (_chunks.Count != chunk.ChunkCount)
		{
			return;
		}

		Restore();
	}

	/// <summary>
	/// The run identity belongs to the connection that announced it: the next
	/// session's host may be serving any run, so the expectation is released with
	/// the buffered set and the first complete set defines it again.
	/// </summary>
	public void ResetSessionState()
	{
		_chunks.Clear();
		_pendingIdentity = null;
		_expectedHostRunEpoch = 0;
		_staleEpochWarned.Clear();
	}

	private void Restore()
	{
		try
		{
			var checkpoint = WireCheckpointAssembler.Assemble([.. _chunks.Values], _codec);
			var result = _authority.Restore(checkpoint);
			if (result.Success)
			{
				_log.LogInformation("Restored kernel checkpoint at revision {Revision} ({Items} items, run {Epoch}).",
					checkpoint.GlobalRevision, checkpoint.Items.Count, checkpoint.RunEpoch.Value);
				if (_expectedHostRunEpoch == 0)
				{
					// No instruction preceded this set (a reconnect's entry group is sent
					// before the join instruction): the restored run IS the run this side
					// serves from here, exactly as an announced one would be.
					_expectedHostRunEpoch = checkpoint.RunEpoch.Value;
					_log.LogInformation("Adopted run {Epoch} from the restored checkpoint (no world-join instruction preceded it).", _expectedHostRunEpoch);
				}
			}
			else
			{
				_log.LogWarning("Kernel checkpoint restore failed: {Message}", result.Error);
			}
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Kernel checkpoint assembly/restore failed for guest.");
		}

		// A restored, refused or unassemblable set is spent: keeping its chunks would
		// only re-attempt the same outcome on the next chunk of the same set.
		_chunks.Clear();
		_pendingIdentity = null;
	}
}
