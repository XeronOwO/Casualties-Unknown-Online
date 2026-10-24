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

## The pixel reading (the residual this record used to leave open)

`tools/acceptance/recipes/capture-frame.cs` captures the client's OWN frame through `ScreenCapture.CaptureScreenshot`; the
agent then reads the PNG. On the same deployed build, at the launcher screen:

| client | captured file | the launcher button reads |
|---|---|---|
| host (`zh`) | host frame, 1280×720 | **`CUO 联机 ▼`** |
| guest (`en`) | guest frame, 1280×720 | **`CUO ONLINE ▼`** |

The frames are gitignored artifacts (`capture-frame` writes to `CUO_FRAME_DIR` or the system temp directory). The guest runs
under Sandboxie, so its capture lands inside the box under `<sandbox-root>\Steam1\drive\…` and the batch copies it out — a fact
for the next visual row, not a failure.

That is the overlay DRAWING the localized string: the same build, the same screen, two languages, both read from the frame.

## The console chat box, drawn (the surface the launcher frames could not show)

The same batch typed `/zzz` into both clients (the driver's console action, so the command really went through the console
service) and captured each frame with the console open:

| client | the chat box line reads |
|---|---|
| host (`zh`) | **未知命令 zzz。输入 /help 查看可用命令。** |
| guest (`en`) | **Unknown command 'zzz'. Type /help for available commands.** |

The window around it is localized to match — 未知伤亡：联机 / 首页 / 玩家 / … on the host, CASUALTIES UNKNOWN: ONLINE / Home /
Players / … on the guest — and both show the same build (`0.1.0+7605a140`).

So the console's keyed line is not merely present in the buffer: it is RENDERED in each client's language, with the typed token
substituted into the sentence, on one build. That is the whole ticket, read from the pixels.

## What this does and does not prove

- **Proves**: the deployed client's language setting drives the lookup; the JSON catalogue is what answers (both languages on
  one build); a console line carries its key in the live buffer; the fallback text is present on the line; the overlay DRAWS
  the localized string — the launcher button reads `CUO 联机` on the Chinese client and `CUO ONLINE` on the English one; and the
  console's chat box renders a keyed refusal in each client's language with the typed token substituted, from the frames above.
- **Residual, named**: the host's answer strings for `/heal`, `/give`, `/summon` and `/gamerule` still travel as sentences over
  the wire, so they stay English until those result messages carry a key — a protocol decision recorded on the console ticket,
  not a gap in the mechanism this record reads.
