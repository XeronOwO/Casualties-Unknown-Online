using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Kernel;

/// <summary>
/// Bounded recently-committed operation store for idempotent retransmission.
/// Only the retransmit window is kept; checkpoints reset the window when they
/// become the new authority.
/// </summary>
internal sealed class CommittedOperationWindow(int capacity)
{
	private readonly int _capacity = capacity;
	private readonly Dictionary<ulong, CommittedBatch> _batches = [];
	private readonly Queue<ulong> _order = new();

	public bool TryGet(OperationId operationId, out CommittedBatch batch) =>
		_batches.TryGetValue(operationId.Value, out batch!);

	public void Add(OperationId operationId, CommittedBatch batch)
	{
		if (_batches.ContainsKey(operationId.Value))
		{
			return;
		}

		_batches[operationId.Value] = batch;
		_order.Enqueue(operationId.Value);
		while (_order.Count > _capacity)
		{
			var oldest = _order.Dequeue();
			_batches.Remove(oldest);
		}
	}

	public void Clear()
	{
		_batches.Clear();
		_order.Clear();
	}
}
