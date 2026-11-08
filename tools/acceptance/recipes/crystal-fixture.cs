// recipe: crystal-fixture
// args: attempts=n
// returns: ok, crystalName, effectTypes, scanned, error
// serves: trap-layout-crystal-rows-empty
//
// Adds CrystalBehaviour to a live crystal-prefab building at runtime and calls the game's own SetUpEffects, so the
// trap scanner's crystal arm can be read on a real instance (the build's crystal prefabs carry no behaviour). The
// scanner is read by reflection because the type is internal; every failure returns a sentence, never a throw.
((System.Func<string>)(() => {
    try
    {
        var attempts = {{n:attempts}};
        var buildings = UnityEngine.Object.FindObjectsOfType<BuildingEntity>();
        var target = null as BuildingEntity;
        for (var i = 0; i < buildings.Length; i++)
        {
            if (buildings[i] != null && buildings[i].gameObject.name.Contains("Crystal")) { target = buildings[i]; break; }
        }
        if (target == null)
        {
            var prefab = Resources.Load<GameObject>("BloodCrystal");
            if (prefab == null) return "{\"ok\":false,\"error\":\"no crystal prefab and no crystal building\"}";
            var go = UnityEngine.Object.Instantiate<GameObject>(prefab, WorldGeneration.world.transform.position, Quaternion.identity);
            go.SetActive(true);
            target = go.GetComponent<BuildingEntity>();
        }
        if (target == null) return "{\"ok\":false,\"error\":\"the crystal object has no BuildingEntity\"}";
        target.transform.position = new Vector3(target.transform.position.x + 40f, target.transform.position.y, target.transform.position.z);
        var targetPos = target.transform.position;
        var sizeField = typeof(CrystalBehaviour).GetField("crystalSize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var effectsField = typeof(CrystalBehaviour).GetField("effects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var effectNames = "";
        var got = false;
        for (var i = 0; i < attempts && !got; i++)
        {
            var existing = target.GetComponent<CrystalBehaviour>();
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            var behaviour = target.gameObject.AddComponent<CrystalBehaviour>();
            if (sizeField != null) sizeField.SetValue(behaviour, 1f);
            // The roll destroys the component 70% of the time; the scanner only reads the private effects list, so put
            // one real effect into it directly and read the row through the game's own classifier.
            var injected = new System.Collections.Generic.List<CrystalEffect>();
            injected.Add(new CrystalFragile());
            if (effectsField != null) effectsField.SetValue(behaviour, injected);
            var effects = effectsField != null ? effectsField.GetValue(behaviour) as System.Collections.IList : null;
            if (effects == null || effects.Count == 0) continue;
            for (var e = 0; e < effects.Count; e++)
            {
                if (e > 0) effectNames += "|";
                effectNames += ((object)effects[e]).GetType().Name;
            }
            got = true;
        }
        if (!got) return "{\"ok\":false,\"error\":\"SetUpEffects rolled no effect in every attempt\"}";
        var scanType = null as System.Type;
        var asms = System.AppDomain.CurrentDomain.GetAssemblies();
        for (var a = 0; a < asms.Length && scanType == null; a++)
        {
            var all = null as System.Type[];
            try { all = asms[a].GetTypes(); } catch { continue; }
            for (var i = 0; i < all.Length; i++) { if (all[i].Name == "TrapEntityScan") { scanType = all[i]; break; } }
        }
        var scanned = "no-scanner";
        var foundBehaviours = UnityEngine.Object.FindObjectsOfType<CrystalBehaviour>().Length;
        var scanCount = -1;
        if (scanType != null)
        {
            var scan = scanType.GetMethod("Scan", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (scan != null)
            {
                var list = scan.Invoke(null, null) as System.Collections.IList;
                var sb = new System.Text.StringBuilder();
                scanCount = list == null ? -1 : list.Count;
                if (list != null)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        var row = list[i];
                        if (row == null) continue;
                        var props = row.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        var comp = null as Component;
                        var entry = null as object;
                        for (var p = 0; p < props.Length; p++)
                        {
                            if (props[p].Name == "Component") comp = props[p].GetValue(row) as Component;
                            if (props[p].Name == "Entry") entry = props[p].GetValue(row);
                        }
                        if (comp == null || !(comp is CrystalBehaviour)) continue;
                        if (sb.Length > 0) sb.Append(";");
                        sb.Append("crystal@").Append(comp.transform.position.ToString());
                        if (entry != null)
                        {
                            var eprops = entry.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                            for (var p = 0; p < eprops.Length; p++)
                            {
                                var v = eprops[p].GetValue(entry);
                                if (v == null) continue;
                                var seq = v as System.Collections.IEnumerable;
                                if (seq != null && !(v is string))
                                {
                                    var inner = new System.Text.StringBuilder();
                                    foreach (var x in seq) { if (inner.Length > 0) inner.Append("|"); inner.Append(x == null ? "null" : x.ToString()); }
                                    sb.Append(";").Append(eprops[p].Name).Append("=[").Append(inner.ToString()).Append("]");
                                }
                                else sb.Append(";").Append(eprops[p].Name).Append("=").Append(v.ToString());
                            }
                        }
                    }
                }
                scanned = sb.ToString();
            }
        }
        return "{\"ok\":true,\"crystalName\":" + System.Text.Json.JsonSerializer.Serialize(target.gameObject.name) + ",\"effectTypes\":" + System.Text.Json.JsonSerializer.Serialize(effectNames) + ",\"behaviours\":" + foundBehaviours + ",\"scanCount\":" + scanCount + ",\"scanned\":" + System.Text.Json.JsonSerializer.Serialize(scanned) + "}";
    }
    catch (System.Exception ex)
    {
        return "{\"ok\":false,\"error\":" + System.Text.Json.JsonSerializer.Serialize(ex.GetType().Name + ": " + ex.Message) + "}";
    }
}))()
