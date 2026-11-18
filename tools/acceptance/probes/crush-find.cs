// recipe: crush-find
// args: radius=n
// serves: unhooked-damage-block-callers, block-damage-table-capacity-alignment
// returns: ok, radius, centerX, centerY, candidates, cells, error
//
// Locates the footstep-crush setup the native code needs: a solid cell whose block type health is
// exactly 1 with both horizontal neighbours also at health 1, inside <radius> cells of the local body.
// The roll's own gate is Body.HandleGroundedState (Body.cs:2594, called at :2587) at Body.cs:2702-2712
// and needs THREE things: the body grounded, `standingOn != null && standingOn.health <= 1f`, and each
// of the three foot-level cells health 1. A candidate here is therefore NECESSARY BUT NOT SUFFICIENT -
// `standingOn` may be a BuildingEntity, and the foot displacement is computed from the collider
// (`col.size.y * 0.5f - col.offset.y + col.edgeRadius + 0.5f`), not from this stand position - so the
// run places the body with `body-place` at cells[].wx/standY and then CONFIRMS the roll from the report
// and the log before any row is judged. cells[] carries x, y (cell), wx, wy (world centre), standY
// (wy + 2, the declared placement aid) and block. Cells outside the world are skipped. Read-only, one eval.
((System.Func<string>)(() => {
	var world = WorldGeneration.world;
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (world == null || body == null) { return "{\"ok\":false,\"error\":\"no-world-or-body\"}"; }
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var radius = (int)({{n:radius}});
	if (radius < 1 || radius > 200) { return "{\"ok\":false,\"error\":\"bad-args\",\"detail\":\"radius must be between 1 and 200\"}"; }
	var center = world.WorldToBlockPos(body.transform.position);
	var sb = new System.Text.StringBuilder();
	var candidates = 0;
	var skippedOutside = 0;
	for (var dy = -radius; dy <= radius && candidates < 8; dy++) {
		for (var dx = -radius; dx <= radius && candidates < 8; dx++) {
			var cell = new Vector2Int(center.x + dx, center.y + dy);
			if (cell.x < 1 || cell.y < 0 || cell.x >= (int)world.width - 1 || cell.y >= (int)world.height) { skippedOutside++; continue; }
			var block = world.GetBlock(cell);
			if (block == 0) { continue; }
			var info = world.GetBlockInfo(block);
			if (info == null || info.health != 1f) { continue; }
			var leftBlock = world.GetBlock(new Vector2Int(cell.x - 1, cell.y));
			var rightBlock = world.GetBlock(new Vector2Int(cell.x + 1, cell.y));
			if (leftBlock == 0 || rightBlock == 0) { continue; }
			var leftInfo = world.GetBlockInfo(leftBlock);
			var rightInfo = world.GetBlockInfo(rightBlock);
			if (leftInfo == null || rightInfo == null || leftInfo.health != 1f || rightInfo.health != 1f) { continue; }
			var worldPos = world.BlockToWorldPos(cell);
			if (candidates > 0) { sb.Append(','); }
			candidates++;
			sb.Append("{\"x\":").Append(cell.x.ToString(inv)).Append(",\"y\":").Append(cell.y.ToString(inv))
				.Append(",\"wx\":").Append(worldPos.x.ToString("0.###", inv))
				.Append(",\"wy\":").Append(worldPos.y.ToString("0.###", inv))
				.Append(",\"standY\":").Append((worldPos.y + 2f).ToString("0.###", inv))
				.Append(",\"block\":").Append(((int)block).ToString(inv)).Append('}');
		}
	}
	return "{\"ok\":true,\"radius\":" + radius.ToString(inv)
		+ ",\"centerX\":" + center.x.ToString(inv) + ",\"centerY\":" + center.y.ToString(inv)
		+ ",\"skippedOutside\":" + skippedOutside.ToString(inv)
		+ ",\"candidates\":" + candidates.ToString(inv) + ",\"cells\":[" + sb.ToString() + "]}";
}))()
