using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The LAYER BOUNDARY's kernel-table policy: which of the kernel's tables describe ONE
/// layer and must therefore start empty for the layer being entered. It exists as its
/// own type because the membership rule is the whole of the policy and it has to be
/// readable in one place — <see cref="WorldService.ResetWorldLayerTables"/> is the SEAM,
/// this is the RULE.
///
/// The rule: a fact about the layer being LEFT must not survive into the layer being
/// entered. Four tables answer to it, and two of them are reset here:
///
/// - the WORLD-ROOTED item rows and the WORLD-ENTITY facts (consumed traps, the durable
///   trap states, opened lockables, building health) are reset by the seam itself —
///   <see cref="IWorldItemLayerReset"/> and the world-fact lifecycle own them;
/// - the ENEMY live rows (a row names both a position in that layer's layout and an id
///   the host allocated from a per-session counter) and the FLUID chunks (coarse totals
///   of a grid that is gone with the old scene) are reset here.
///
/// The PLAYER table is deliberately NOT in the family: every fact it holds is
/// cross-layer (alive/conscious, the carry relation, the limb latches, body state,
/// skills), so resetting it would erase a player's injuries and their carry relation on
/// every descent. The enemy TOMBSTONES are kept for the same reason one level down — a
/// terminal fact the killer earned, which is what stops a stale live row from
/// resurrecting the id (see <c>EnemyStateTable.WithoutLiveEnemies</c>).
///
/// <para>
/// The SEAM this rule is applied at is the world-entry reset, not the layer-end cut's
/// kernel commit. That cut is taken while the old layer's scene is STILL LIVE, and
/// <c>EnemyKernelProjection.Sync</c> maintains the kernel enemy table as a mirror of the
/// live scene every frame — so a reset at the cut would be written back frame by frame
/// (and would churn destroyed/recreated frozen copies on every guest) while buying
/// nothing. At world entry the old scene is gone, so the tables are empty at the moment
/// the new layer becomes observable: a late joiner's checkpoint and a restore read the
/// truth.
/// </para>
///
/// Host/solo only: a guest's kernel is the host's replay, so it must not commit its own
/// reset — it learns about the boundary from the host's committed batch and its
/// checkpoints.
/// </summary>
internal sealed class LayerScopedTableReset(
	ISessionControl session,
	ItemKernelAuthority kernelAuthority,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ILogger _log = log;

	/// <summary>
	/// Drop the two tables this type owns, for the layer being entered. A refused reset
	/// is logged rather than thrown: aborting a world generation over one table reset is
	/// far worse than the reset itself, the next boundary retries it, and the operator
	/// sees which table refused and why. Nothing is written when both tables are already
	/// empty, so an ordinary new run costs no log line and no kernel commit.
	/// </summary>
	internal void Reset()
	{
		if (_session.Role == SessionRole.Guest)
		{
			return;
		}

		var enemies = _kernelAuthority.QueryEnemies()?.Enemies.Count ?? 0;
		var fluids = _kernelAuthority.QueryFluids()?.Regions.Count ?? 0;
		if (enemies == 0 && fluids == 0)
		{
			return;
		}

		if (!_kernelAuthority.TryResetEnemies(_session.LocalSteamId, out _, out var enemyRejection))
		{
			_log.LogWarning("[LayerReset] the enemy reset for the new layer was rejected: {Reason} ({Message}).",
				enemyRejection!.Reason, enemyRejection.Message);
		}

		if (!_kernelAuthority.TryResetFluids(_session.LocalSteamId, out _, out var fluidRejection))
		{
			_log.LogWarning("[LayerReset] the fluid reset for the new layer was rejected: {Reason} ({Message}).",
				fluidRejection!.Reason, fluidRejection.Message);
		}

		_log.LogInformation(
			"[LayerReset] dropped the previous layer's {Enemies} enemy row(s) and {Fluids} fluid chunk(s); they describe the layer the world is leaving.",
			enemies, fluids);
	}
}
