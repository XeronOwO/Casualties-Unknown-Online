// recipe: locale-state
// args: none
// returns: ok, dataPath, preference, current, file, labels
// serves: acceptance-row-review-campaign
//
// The game loads its texts from <dataPath>/Lang/<preference>.json; when that file cannot be read it keeps an empty
// language and every label draws as its own key — the placeholders the sandboxed client showed while no row noticed.
// This reads the facts that decide it: the path the game resolved, the three ways the name can be known, and how many
// labels the loaded language actually carries (zero is the defect).
((System.Func<string>)(() => {
    var statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
    var instances = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
    var nameField = typeof(Locale).GetField("currentLangName", statics);
    var current = nameField == null ? "" : ((nameField.GetValue(null) as string) ?? "");
    var preference = UnityEngine.PlayerPrefs.GetString("locale", "");
    var resolved = string.IsNullOrEmpty(current) ? (string.IsNullOrEmpty(preference) ? "EN" : preference) : current;
    var path = UnityEngine.Application.dataPath + "/Lang/" + resolved + ".json";
    var labels = 0;
    var langField = typeof(Locale).GetField("currentLang", statics);
    var language = langField == null ? null : langField.GetValue(null);
    if (language != null) {
        var main = language.GetType().GetProperty("main", instances);
        var dictionary = main == null ? null : main.GetValue(language) as System.Collections.IDictionary;
        labels = dictionary == null ? 0 : dictionary.Count;
    }
    return "{\"ok\":true,\"dataPath\":\"" + UnityEngine.Application.dataPath.Replace("\\", "/") + "\",\"preference\":\"" + preference + "\",\"current\":\"" + current + "\",\"file\":" + (System.IO.File.Exists(path) ? "true" : "false") + ",\"labels\":" + labels + "}";
}))()
