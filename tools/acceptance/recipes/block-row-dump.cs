// recipe: block-row-dump
// args: y=n width=n
// returns: ok, y, width, values, error
//
// The live block table's row as a compact string: two hex digits per cell, left to right, so two
// clients' answers for the same row can be compared column by column. Read-only.
((System.Func<string>)(() => {
var world = WorldGeneration.world;
if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
var y = (int)({{n:y}});
var width = (int)({{n:width}});
if (width <= 0 || y < 0) { return "{\"ok\":false,\"error\":\"bad-args\"}"; }
var inv = System.Globalization.CultureInfo.InvariantCulture;
var sb = new System.Text.StringBuilder(width * 2);
for (var x = 0; x < width; x++) {
var v = (int)world.GetBlock(new Vector2Int(x, y));
if (v < 0) { v = 0; }
if (v > 255) { v = 255; }
sb.Append(v.ToString("X2", inv));
}
return "{\"ok\":true,\"y\":" + y.ToString(inv)
+ ",\"width\":" + width.ToString(inv)
+ ",\"values\":\"" + sb.ToString() + "\"}";
}))()
