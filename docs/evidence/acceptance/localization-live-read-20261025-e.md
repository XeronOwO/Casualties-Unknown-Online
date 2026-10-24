# The deployed client's own language, read live

- Tickets: `docs/backlog/done/console-command-replies-bilingual.md` (its in-game read) and
  `docs/backlog/done/localization-json-resources-and-mod-languages.md` (step 7)
- Batch: `20261025-e` — the clients launched on the deployed artifact `0.1.0+7605a140…` (the plugin's own load line)
- Instrument: `tools/acceptance/recipes/ui-text.cs` — reads the LIVE localization service inside each client and what it
  answers for the keys the window shows, plus the command console's last line with its key and its English text

## How the probe reaches the service

The plugin adds its host as a component on the same GameObject (`Plugin.Awake`), the host holds the DI provider, and the
provider resolves `ILocalizationService` and `CommandConsoleService`. The probe walks those by name with a named refusal at
every step (`no-plugin-type`, `no-plugin-instance`, `no-service-provider`, `no-localization-service`), so a renamed type fails
loudly. Two earlier attempts were `no-plugin-instance` (the plugin component was on an inactive object, so `FindObjectOfType`
missed it — `Resources.FindObjectsOfTypeAll` sees it) and `no-service-provider` (the host is a component, not a field).

## The reading

Host (the acceptance runs use Chinese on the host):

| field | value |
|---|---|
| `language` | `zh` |
| `supported` | `en,zh` |
| `T("tab.home")` | 首页 |
| `T("console.ready")` | CUO 命令控制台已就绪。输入 /help 查看可用命令。 |
| `T("prefs.language")` | 语言 |
| console lines | 1, its key `console.ready`, its stored English text the catalogue's English sentence |

Guest (English on the guest):

| field | value |
|---|---|
| `language` | `en` |
| `supported` | `en,zh` |
| `T("tab.home")` | Home |
| `T("console.ready")` | CUO command console ready. Type /help for available commands. |
| console lines | 1, key `console.ready`, English text |

So the same key answers in two languages on two clients of the same build, from the embedded JSON catalogue, and the console
line that carries that key is present in the live console buffer with its key and its English fallback intact.

## What this does and does not prove

- **Proves**: the deployed client's language setting drives the lookup; the JSON catalogue is what answers (both languages on
  one build); a console line carries its key in the live buffer, which is the data the chat box renders; and the fallback text
  is present on the line.
- **Does not prove**: the pixels — that the overlay drew the Chinese sentence — which stays a frame-read residual for the user,
  and is the same capability gap the visual rows carry.
