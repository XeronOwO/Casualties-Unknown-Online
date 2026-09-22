using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The kernel-replication block of the composition root: the item kernel
/// authority, the admission seam that reads it, the host's refused-creation
/// tombstones, the guest's pending-command reconciliation and the Phase C
/// four-envelope protocol service — plus the Application-layer ports the
/// replication surface is reached through.
///
/// <para>
/// It lives in its own file for the same reason the content-vocabulary block
/// does — <c>CuoBootstrap</c> sits on its architecture line cap — and the
/// registrations it holds, and their order, are unchanged from the block that
/// was there, so the <see cref="ICuoService"/> update order does not move. The
/// port lines come last: none of them is an <see cref="ICuoService"/>, they only
/// name which Runtime singleton answers each capability.
/// </para>
/// </summary>
internal static class KernelReplicationComposition
{
	/// <summary>Registers the kernel replication feature and the ports it reads the session, the transport and the kernel through.</summary>
	internal static void AddKernelReplication(IServiceCollection services)
	{
		services.AddSingleton<ItemKernelAuthority>();
		// The Application layer's admission seam (see KernelCommandGateway); it reads the kernel through IKernelItemFacts.
		services.AddSingleton<IKernelItemFacts>(p => p.GetRequiredService<ItemKernelAuthority>());
		services.AddSingleton<KernelCommandGateway>();
		// The host's tombstones for item ids whose creation it refused: shared by
		// the kernel command path (which answers a later operation with the precise
		// reason) and the item domain (which records the refusal).
		services.AddSingleton<RefusedItemCreations>();
		// Phase C four-envelope kernel protocol: executes wire commands on the
		// host, applies checkpoints/batches on the guest, and owns the host
		// journal used by join/reconnect fallback.
		// Guest-side item-command convergence (sync-coverage audit row I5): the bounded
		// re-report window that heals a swallowed item command. Its own time edge — the
		// short-window family, like SessionControlConvergence above, not the unbounded
		// pending-report tables.
		services.AddSingleton<GuestCommandReconciliation>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GuestCommandReconciliation>());
		services.AddSingleton<KernelProtocolService>();
		services.AddSingleton<IKernelProtocolControl>(p => p.GetRequiredService<KernelProtocolService>());
		// The replication surface's ports. The kernel authority answers four
		// capabilities (read, execute, checkpoint, apply) and the session, the
		// transport, the pending-command table and the item-data normalizer answer one each.
		services.AddSingleton<IKernelCommandExecution>(p => p.GetRequiredService<ItemKernelAuthority>());
		services.AddSingleton<IKernelCheckpointSource>(p => p.GetRequiredService<ItemKernelAuthority>());
		services.AddSingleton<IKernelBatchApplication>(p => p.GetRequiredService<ItemKernelAuthority>());
		services.AddSingleton<IKernelPendingCommands>(p => p.GetRequiredService<GuestCommandReconciliation>());
		services.AddSingleton<IKernelSessionFacts>(p => p.GetRequiredService<SessionService>());
		services.AddSingleton<IKernelFrameSender>(p => p.GetRequiredService<PacketSender>());
		services.AddSingleton<IKernelItemDataNormalizer, KernelItemDataNormalizer>();
	}
}
