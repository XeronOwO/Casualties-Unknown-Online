// recipe: cuo-button-click
// args: label=s
// returns: ok, matched, invoked, labels, detail
//
// Clicks a CUO window button the way a player does: finds the uGUI Button whose
// caption contains the given text and invokes its onClick, so the game's own
// listener (the one that reports the intent and plays the click) runs - unlike
// the driver's click action, which enqueues the intent directly.
// Reflection only: the evaluator cannot name the UI assemblies' types.
((System.Func<string>)(() =>
{
	var wanted = {{s:label}};
	System.Type buttonType = null;
	System.Type textType = null;
	foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
	{
		buttonType = buttonType ?? assembly.GetType("UnityEngine.UI.Button");
		textType = textType ?? assembly.GetType("TMPro.TMP_Text");
	}

	if (buttonType == null)
	{
		return "{\"ok\":false,\"detail\":\"no-button-type\"}";
	}

	var buttons = UnityEngine.Object.FindObjectsOfType(buttonType, true);
	var matched = 0;
	var invoked = 0;
	var labels = new System.Collections.Generic.List<string>();
	var onClickProperty = buttonType.GetProperty("onClick");
	foreach (var candidate in buttons)
	{
		var caption = "";
		if (textType != null)
		{
			var texts = (candidate as UnityEngine.Component).GetComponentsInChildren(textType, true);
			foreach (var text in texts)
			{
				var value = textType.GetProperty("text")?.GetValue(text) as string;
				if (!string.IsNullOrEmpty(value))
				{
					caption = value;
					break;
				}
			}
		}

		if (caption.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) < 0)
		{
			continue;
		}

		matched++;
		labels.Add(caption);
		var onClick = onClickProperty?.GetValue(candidate);
		if (onClick != null)
		{
			onClick.GetType().GetMethod("Invoke")?.Invoke(onClick, null);
			invoked++;
		}
	}

	return "{\"ok\":true,\"matched\":" + matched + ",\"invoked\":" + invoked + ",\"labels\":\"" + string.Join("|", labels) + "\"}";
}))()
