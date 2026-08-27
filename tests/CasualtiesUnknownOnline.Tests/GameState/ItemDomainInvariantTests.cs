using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class ItemDomainInvariantTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);
	private static readonly ActorId Guest = new(2001);

	[Fact]
	public void RandomOperationSequence_NeverViolatesItemInvariants()
	{
		var kernel = new GameStateKernel(Epoch);
		var random = new System.Random(1234);
		var known = new HashSet<ulong>();
		var terminal = new HashSet<ulong>();
		var op = 0ul;

		for (var i = 0; i < 500; i++)
		{
			op++;
			if (known.Count < 60 || random.Next(2) == 0)
			{
				var id = (ulong)(known.Count + 1);
				known.Add(id);
				var spawn = Spawn(kernel, op, id, $"item-{id}");
				Assert.True(spawn.IsAccepted);
			}
			else
			{
				var live = new List<ulong>();
				foreach (var existingId in known)
				{
					if (!terminal.Contains(existingId))
					{
						live.Add(existingId);
					}
				}

				if (live.Count == 0)
				{
					continue;
				}

				var id = live[random.Next(live.Count)];
				var current = kernel.FindItem(id)!.Value;
				var action = random.Next(3);
				if (action == 0 && current.Location.Kind == ItemLocationKind.World)
				{
					kernel.Execute(
						new PickUpItemCommand(new OperationId(op), Guest, Epoch, AuthorityKind.OwnerPredictedHostValidated, id, Guest, current.Revision),
						new CommandContext(Epoch, Guest));
				}
				else if (action == 1 && current.Location.Kind == ItemLocationKind.Carried)
				{
					kernel.Execute(
						new DropItemCommand(new OperationId(op), current.Location.Owner, Epoch, AuthorityKind.OwnerPredictedHostValidated, id, ItemLocation.World(10, 20), current.Revision),
						new CommandContext(Epoch, current.Location.Owner));
				}
				else
				{
					var destroy = kernel.Execute(
						new DestroyItemCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, id, TerminalKind.Destroyed, current.Revision),
						new CommandContext(Epoch, Host));
					if (destroy.IsAccepted)
					{
						terminal.Add(id);
					}
				}
			}

			AssertInvariants(kernel);
		}
	}

	private static Decision Spawn(GameStateKernel kernel, ulong op, ulong id, string definition) =>
		kernel.Execute(
			new SpawnItemCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, new ItemIdentity(id, definition), ItemLocation.World(1, 2), 0),
			new CommandContext(Epoch, Host));

	private static void AssertInvariants(GameStateKernel kernel)
	{
		var items = kernel.QueryItems();
		var ids = new HashSet<ulong>();
		foreach (var item in items.Values)
		{
			Assert.True(ids.Add(item.Identity.InstanceId), $"duplicate instance id {item.Identity.InstanceId}");
			Assert.True(item.Revision > 0, $"item {item.Identity.InstanceId} has revision 0");
			if (item.Location.Kind == ItemLocationKind.Carried)
			{
				Assert.NotEqual(0ul, item.Location.Owner.Value);
			}

			if (item.Location.Kind == ItemLocationKind.Contained)
			{
				Assert.NotEqual(0ul, item.Location.ParentItemId);
			}
		}
	}
}
