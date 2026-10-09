// recipe: building-template-inject
// args: mode=s id=s template=s owner=s
// serves: runtime-entity-spawn-backfill rows 11 and 12, runtime-entity-creation-rejection rows 1-3 and 6
// returns: ok, mode, id, accepted, hasDefinition, hasTemplate, failed, vanilla, templates, error, detail
//
// Binds one ModBuildingDefinition into the live GameAdapter building-content provider through the same
// public TryBind seam the mod content binder uses, so a run can stage a runtime template on exactly the
// clients it chooses: the host lacking it (a reported creation the host cannot materialize) or every
// client carrying it (materialization with no Resources.Load pre-check). mode=bind registers the
// definition once (idempotent); mode=status reads the provider's own dictionaries. The template itself is
// built by the provider's Update once this client is inside a world, so read status one command later.
((System.Func<string>)(() => {
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var provider = services.GetService(typeof(CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterBuildingContentProvider)) as CasualtiesUnknownOnline.GameAdapter.Content.GameAdapterBuildingContentProvider;
	if (provider == null) { return "{\"ok\":false,\"error\":\"no-building-provider\"}"; }
	var mode = {{s:mode}};
	var id = {{s:id}};
	var template = {{s:template}};
	var owner = {{s:owner}};
	if (mode != "bind" && mode != "status") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be bind or status\"}";
	}
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var type = provider.GetType();
	var definitions = type.GetField("_definitions", flags).GetValue(provider) as System.Collections.IDictionary;
	var accepted = false;
	var detail = "status";
	if (definitions != null && definitions.Contains(id)) {
		accepted = true;
		detail = "already-bound";
	}
	else if (mode == "bind") {
		var definition = new CasualtiesUnknownOnline.Abstractions.ModBuildingDefinition();
		definition.Id = id;
		definition.DisplayName = "Acceptance Runtime Template";
		definition.Description = "acceptance-only runtime building template";
		definition.TemplateId = template;
		var registration = new CasualtiesUnknownOnline.Runtime.Session.Mods.ModContentRegistration(owner, definition, null);
		accepted = provider.TryBind(registration);
		detail = accepted ? "bound" : "refused";
	}
	var templates = type.GetField("_templates", flags).GetValue(provider) as System.Collections.IDictionary;
	var failedSet = type.GetField("_templateFailures", flags).GetValue(provider);
	var vanillaSet = type.GetField("_vanillaIds", flags).GetValue(provider);
	var failed = failedSet != null && (bool)failedSet.GetType().GetMethod("Contains").Invoke(failedSet, new object[] { id });
	var vanilla = vanillaSet != null && (bool)vanillaSet.GetType().GetMethod("Contains").Invoke(vanillaSet, new object[] { id });
	var templateCount = templates == null ? -1 : templates.Count;
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"id\":\"" + id + "\""
		+ ",\"accepted\":" + (accepted ? "true" : "false")
		+ ",\"hasDefinition\":" + (definitions != null && definitions.Contains(id) ? "true" : "false")
		+ ",\"hasTemplate\":" + (templates != null && templates.Contains(id) ? "true" : "false")
		+ ",\"failed\":" + (failed ? "true" : "false")
		+ ",\"vanilla\":" + (vanilla ? "true" : "false")
		+ ",\"templates\":" + templateCount.ToString(inv)
		+ ",\"detail\":\"" + detail + "\"}";
}))()
