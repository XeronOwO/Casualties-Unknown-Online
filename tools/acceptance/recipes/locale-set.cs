// recipe: locale-set
// args: name=s
// returns: ok, name, file, labels
// serves: acceptance-row-review-campaign
//
// A client whose `locale` preference names a language file that is not on disk keeps an empty language and draws every
// label as its own key. This sets the preference and drops the cached language so the next lookup reloads it, then
// reports how many labels the file actually carries — zero means the screen is still unreadable and the run must stop.
((System.Func<string, string>)((name) => {
    var statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
    UnityEngine.PlayerPrefs.SetString("locale", name);
    UnityEngine.PlayerPrefs.Save();
    var nameField = typeof(Locale).GetField("currentLangName", statics);
    if (nameField != null) { nameField.SetValue(null, ""); }
    var langField = typeof(Locale).GetField("currentLang", statics);
    if (langField != null) { langField.SetValue(null, null); }
    var reload = typeof(Locale).GetMethod("LoadLanguage", statics);
    if (reload != null) { reload.Invoke(null, null); }
    var current = nameField == null ? "" : ((nameField.GetValue(null) as string) ?? "");
    var path = UnityEngine.Application.dataPath + "/Lang/" + name + ".json";
    var labels = 0;
    var language = langField == null ? null : langField.GetValue(null);
    if (language != null) {
        var type = language.GetType();
        var field = type.GetField("main", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var property = field == null ? type.GetProperty("main", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) : null;
        var value = field != null ? field.GetValue(language) : (property == null ? null : property.GetValue(language));
        var dictionary = value as System.Collections.IDictionary;
        labels = dictionary == null ? 0 : dictionary.Count;
    }
    return "{\"ok\":true,\"name\":\"" + name + "\",\"current\":\"" + current + "\",\"file\":" + (System.IO.File.Exists(path) ? "true" : "false") + ",\"labels\":" + labels + "}";
}))({{s:name}})
