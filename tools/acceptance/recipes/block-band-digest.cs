// recipe: block-band-digest
// args: y0=n y1=n width=n
// returns: ok, y0, y1, width, rows, error
//
// One FNV-1a digest per world row across a band of rows, over the live block table. The world
// fingerprint logs eight chunks of 128 rows; when one chunk differs, this narrows the difference to
// rows and then (with `block-read-at`) to cells. Read-only.
((System.Func<string>)(() => {
var world = WorldGeneration.world;
if (world == null) { return "{\"ok\":false,\"error\":\"no-world\"}"; }
var y0 = (int)({{n:y0}});
var y1 = (int)({{n:y1}});
var width = (int)({{n:width}});
if (width <= 0 || y0 < 0 || y1 <= y0) { return "{\"ok\":false,\"error\":\"bad-args\"}"; }
var inv = System.Globalization.CultureInfo.InvariantCulture;
var sb = new System.Text.StringBuilder();
sb.Append("{\"ok\":true,\"y0\":").Append(y0.ToString(inv));
sb.Append(",\"y1\":").Append(y1.ToString(inv));
sb.Append(",\"width\":").Append(width.ToString(inv));
sb.Append(",\"rows\":[");
for (var y = y0; y < y1; y++) {
if (y > y0) { sb.Append(','); }
ulong h = 14695981039346656037UL;
for (var x = 0; x < width; x++) {
var v = (ulong)((int)world.GetBlock(new Vector2Int(x, y)));
h ^= v;
h *= 1099511628211UL;
}
sb.Append('"').Append(h.ToString("X16", inv)).Append('"');
}
sb.Append("]}");
return sb.ToString();
}))()
