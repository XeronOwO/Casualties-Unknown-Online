// recipe: select-character
// args: index=n
// returns: ok, index, spawn
// serves: 1.0-remote-players-carry-no-character
//
// Sets the game's own character choice (PreRunScript.characterSelected is the field the pre-run
// screen's portrait writes; PlayerCamera.Awake reads it when it spawns the run's body), so the next
// spawn is that character. Returns the index the field now holds and the prefab the game's own spawn
// table would load for it, which is the fact the remote clone is judged against.
((System.Func<string>)(() => {
    PreRunScript.characterSelected = {{n:index}};
    var chr = PreRunScript.characterSelected;
    var path = chr == 0 ? "Special/Characters/Experiment" : (chr == 1 ? "Special/Characters/Milky" : "Special/Characters/Dune");
    return "{\"ok\":true,\"index\":" + chr + ",\"spawn\":\"" + path + "\"}";
}))()
