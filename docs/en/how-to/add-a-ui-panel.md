# Add a panel to the interface

[Documentation](../../README.md) > [How to](README.md) > Add a panel to the interface

**After this page** your mod has its own window inside the CUO interface, and you know what that
window may and may not touch. Read [Your first mod](../start/your-first-mod.md) first.

## What a mod window is

`IModUi` is a per-mod, local, immediate-mode window registry. You register an id, a title and a draw
callback in `Bind`; CUO calls the callback every frame while the interface is open, and the plugin
owns every Unity and IMGUI detail. No Unity type reaches your code.

- **No permission is needed.** A window cannot touch the network, the session or authoritative state
  by itself, so every network mode may use it.
- **The window is presentation only.** Shared state still travels through the mod network or a host
  command; the window projects what the mod already knows.
- **The control set is deliberately tiny**: `Label`, `Button` (true on the click frame),
  `TextField` (returns the edited text) and `Separator`.

## Register a window

```csharp
context.Ui.Register("status", "My Mod Status", window =>
{
	window.Label($"session active: {context.Session.SessionActive}");

	if (window.Button("ping"))
	{
		context.Network.Broadcast(Encoding.UTF8.GetBytes("ping"));
	}

	_text = window.TextField(_text);
});
```

The callback runs every frame, so it must be cheap and must not allocate more than it needs to. The
mod owns any value it wants to keep between frames — the example stores the text field's value in a
field of its own.

## Rules and failure isolation

- An empty id or title, or a `null` callback, is refused; so is a second window with the same id in
  the same mod.
- `context.Ui.Unregister("status")` removes the window.
- A callback that throws shows an inline error in the window and is logged; it never breaks the
  interface frame for everyone else.

## Check that it worked

Open the CUO interface: the window is there with its title, and its state matches the session —
`session active: False` before you host or join, `True` afterwards. Press the button and watch the
log line from the message handler.

## Traps

- **Do not keep state in the callback's closure and expect it to survive.** Store it in a field; the
  callback is called again on the next frame.
- **Do not do the work in the window.** A window that sends messages on every frame will hit the
  send rate limit; send on a click or a state change.

## Related reading

- [Send a message to the other players](send-a-network-message.md) — the call the button makes
- [Declare permissions and host commands](declare-permissions-and-commands.md) — the other surface a
  window may drive
- [Your first mod](../start/your-first-mod.md) — where `Bind` and the context come from

[Documentation](../../README.md) > [How to](README.md) > Add a panel to the interface
