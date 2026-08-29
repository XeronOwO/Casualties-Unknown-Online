using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Splits and reassembles a <see cref="GameCheckpoint"/> into
/// <see cref="WireCheckpoint"/> chunks. The host sends chunks in order; the
/// guest restores only after every chunk has arrived.
/// </summary>
public static class WireCheckpointAssembler
{
	public static IReadOnlyList<WireCheckpoint> Split(GameCheckpoint checkpoint)
	{
		var chunks = new List<WireCheckpoint>();
		var items = checkpoint.Items;
		var chunkCount = Math.Max(1, (items.Count + ProtocolConstants.CheckpointChunkItemCount - 1) / ProtocolConstants.CheckpointChunkItemCount);
		for (var index = 0; index < chunkCount; index++)
		{
			var slice = items.Skip(index * ProtocolConstants.CheckpointChunkItemCount)
				.Take(ProtocolConstants.CheckpointChunkItemCount)
				.Select(KernelWireMapper.ToWireItem)
				.ToList();
			chunks.Add(new WireCheckpoint
			{
				ChunkIndex = index,
				ChunkCount = chunkCount,
				RunEpoch = checkpoint.RunEpoch.Value,
				GlobalRevision = checkpoint.GlobalRevision,
				Items = slice,
				RandomStreams = index == 0
					? [.. checkpoint.RandomStreams?.Select(KernelWireMapper.ToWireRandomStream) ?? []]
					: [],
				Run = index == 0 && checkpoint.Run is not null
					? KernelDomainWireMapper.ToWireRun(checkpoint.Run)
					: null,
				WorldEntities = index == 0 && checkpoint.WorldEntities is not null
					? KernelDomainWireMapper.ToWireWorldEntityState(checkpoint.WorldEntities)
					: null,
				Players = index == 0
					? [.. (checkpoint.Players?.Players ?? []).Select(KernelDomainWireMapper.ToWirePlayerState)]
					: [],
				Enemies = index == 0
					? [.. (checkpoint.Enemies?.Enemies ?? []).Select(KernelDomainWireMapper.ToWireEnemyState)]
					: [],
				Fluids = index == 0
					? [.. (checkpoint.Fluids?.Regions ?? []).Select(KernelDomainWireMapper.ToWireFluidRegionState)]
					: [],
			});
		}

		return chunks;
	}

	public static GameCheckpoint Assemble(IReadOnlyList<WireCheckpoint> chunks)
	{
		if (chunks.Count == 0)
		{
			throw new ArgumentException("Checkpoint must contain at least one chunk.", nameof(chunks));
		}

		var first = chunks[0];
		var count = first.ChunkCount;
		var ordered = new WireCheckpoint[count];
		foreach (var chunk in chunks)
		{
			if (chunk.ChunkCount != count)
			{
				throw new InvalidOperationException($"Checkpoint chunk {chunk.ChunkIndex} declares {chunk.ChunkCount} chunks; expected {count}.");
			}

			if (chunk.RunEpoch != first.RunEpoch || chunk.GlobalRevision != first.GlobalRevision)
			{
				throw new InvalidOperationException("Checkpoint chunks disagree on epoch or global revision.");
			}

			if (chunk.ChunkIndex < 0 || chunk.ChunkIndex >= count || ordered[chunk.ChunkIndex] is not null)
			{
				throw new InvalidOperationException($"Invalid or duplicate checkpoint chunk index {chunk.ChunkIndex}.");
			}

			ordered[chunk.ChunkIndex] = chunk;
		}

		if (ordered.Any(c => c is null))
		{
			throw new InvalidOperationException("Checkpoint is incomplete; at least one chunk is missing.");
		}

		var items = ordered
			.SelectMany(c => c!.Items)
			.Select(KernelWireMapper.FromWireItem)
			.ToList();

		var randomStreams = ordered[0]!.RandomStreams
			.Select(KernelWireMapper.FromWireRandomStream)
			.ToList();

		var firstRun = ordered[0]!.Run;
		var run = firstRun is null ? null : KernelDomainWireMapper.FromWireRun(firstRun);
		var firstWorldEntities = ordered[0]!.WorldEntities;
		var worldEntities = firstWorldEntities is null ? null : KernelDomainWireMapper.FromWireWorldEntityState(firstWorldEntities);
		var players = ordered[0]!.Players.Count == 0
			? null
			: new PlayerStateTable([.. ordered[0]!.Players.Select(KernelDomainWireMapper.FromWirePlayerState)]);
		var enemies = ordered[0]!.Enemies.Count == 0
			? null
			: new EnemyStateTable([.. ordered[0]!.Enemies.Select(KernelDomainWireMapper.FromWireEnemyState)]);
		var fluids = ordered[0]!.Fluids.Count == 0
			? null
			: new FluidStateTable([.. ordered[0]!.Fluids.Select(KernelDomainWireMapper.FromWireFluidRegionState)]);
		return new GameCheckpoint(new RunEpoch(first.RunEpoch), first.GlobalRevision, items, randomStreams, run, worldEntities, players, enemies, fluids);
	}
}
