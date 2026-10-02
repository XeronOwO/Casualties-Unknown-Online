// recipe: enemy-table-read
// args: none
// serves: enemy-snapshot-binding-recovery row 2 (the host's enemy table at a repair-send moment; the member's own set across it)
// returns: ok, utc, count, runtimeSpawns, enemies, error, detail
//
// Reads THIS client's runtime enemy table — EnemySyncService's buffered authoritative set, the exact
// collection SendEnemySnapshot checks (`_enemies.Count == 0` returns without a frame or a log line).
// The host's per-frame capture replaces the table with the live scene's animals, so a count of 0 during
// the host's own world/layer generation IS the product's empty-table state, not a probe invention; on a
// member the same read shows the set an arriving snapshot would replace. Read-only; one eval.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var service = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.EntitySync.EnemySyncService));
	if (service == null) { return "{\"ok\":false,\"error\":\"no-enemy-service\"}"; }
	var enemies = service as CasualtiesUnknownOnline.Runtime.Session.EntitySync.EnemySyncService;
	if (enemies == null) { return "{\"ok\":false,\"error\":\"service-not-enemy-sync\"}"; }
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var count = 0;
	var sb = new System.Text.StringBuilder();
	foreach (var enemy in enemies.Enemies) {
		if (enemy == null) { continue; }
		if (count > 0) { sb.Append(','); }
		count = count + 1;
		sb.Append("{\"epoch\":").Append(enemy.EntityId.Epoch.ToString(inv))
			.Append(",\"counter\":").Append(enemy.EntityId.Counter.ToString(inv))
			.Append(",\"generation\":").Append(enemy.EntityId.Generation.ToString(inv))
			.Append(",\"prefab\":\"").Append(enemy.PrefabId).Append('"')
			.Append(",\"health\":").Append(enemy.Health.ToString("0.###", inv))
			.Append(",\"x\":").Append(enemy.Position.X.ToString("0.###", inv))
			.Append(",\"y\":").Append(enemy.Position.Y.ToString("0.###", inv))
			.Append(",\"spawnX\":").Append(enemy.SpawnPosition.X.ToString("0.###", inv))
			.Append(",\"spawnY\":").Append(enemy.SpawnPosition.Y.ToString("0.###", inv))
			.Append(",\"runtimeSpawned\":").Append(enemy.RuntimeSpawned ? "true" : "false")
			.Append('}');
	}
	return "{\"ok\":true,\"utc\":\"" + System.DateTime.UtcNow.ToString("o") + "\",\"count\":" + count.ToString(inv)
		+ ",\"runtimeSpawns\":" + enemies.RuntimeSpawns.Count.ToString(inv)
		+ ",\"enemies\":[" + sb.ToString() + "]}";
}))()
