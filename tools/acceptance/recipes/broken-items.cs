// recipe: broken-items
// args: none
// returns: ok, count, rows, error, detail
//
// Lists the game's Item objects and, for each, the object it is on, whether it still has a rigidbody
// and whether the world it reads exists. The guest-join ticket
// (`docs/backlog/review/1.0-guest-joins-with-a-role-and-no-world.md`) needs this to name what the
// `Item.Update` flood's items hang on while the client sits at the main menu.

((System.Func<string>)(() => {
	var sb = new System.Text.StringBuilder();
	var n = 0;
	foreach (var o in UnityEngine.Object.FindObjectsOfType(typeof(Item)))
	{
		var item = (Item)o;
		if (item == null)
		{
			continue;
		}

		n++;
		if (n > 20)
		{
			break;
		}

		var parent = item.transform.parent != null ? item.transform.parent.name : "-";
		sb.Append(item.gameObject.name).Append('|').Append(parent).Append('|')
			.Append(item.transform.root.name).Append('|')
			.Append(item.GetComponent<Rigidbody2D>() == null ? "noRb" : "rb").Append('|')
			.Append(WorldGeneration.world == null ? "noWorld" : "world").Append(';');
	}

	return "{\"ok\":true,\"count\":" + n.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"rows\":\"" + sb.ToString().Replace("\"", "'") + "\"}";
}))()
