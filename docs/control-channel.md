# Local Control Channel

VidShrink can be driven from another process on the same machine through its single-instance named pipe.
The channel is **off by default**. It carries one JSON command per line and answers with one JSON reply per line.

## Enabling

Start the app with `--denetim-kanali` (or `--control-channel`). Without the flag every command gets
`{"ok":false,"error":"disabled"}` and nothing runs. There is no visible setting for it.

The flag only counts on the instance that owns the pipe. If VidShrink is already running without the flag,
a second launch with the flag only forwards its file paths and the channel stays off; close the app first.

## Transport and security

- Pipe name: `VidShrinkTekOrnek-<channel>`, where `<channel>` is the per-user single-instance channel
  (`SingleInstanceChannel.DefaultChannel()`).
- The pipe is created with `PipeOptions.CurrentUserOnly`: only processes of the same user can connect.
- No network socket is opened. No new package is used.
- One connection carries exactly one request line and one reply line (UTF-8, no BOM, `\n`).
- The old single-instance message (a JSON array of paths) still works on the same pipe; a line that starts
  with `{` is a command, anything else is the old path list.

## Commands

| `command`        | Fields                          | Effect                                                       |
|------------------|---------------------------------|--------------------------------------------------------------|
| `status`         |                                 | Returns the status object.                                   |
| `play`           |                                 | Starts playback of the loaded media.                         |
| `pause`          |                                 | Pauses playback.                                             |
| `seek`           | `seconds` (number, ≥ 0)         | Moves the player to the given position.                      |
| `open`           | `path` (file or media address)  | Opens the file in the player, like a forwarded path.         |
| `record-start`   |                                 | Switches to the Recorder tab and starts a recording.         |
| `record-stop`    |                                 | Stops the running recording.                                 |
| `enqueue-shrink` | `path`, `targetMb` (integer)    | Queues a shrink, same rules as the shell menu targets.       |

Every command may carry an `id` string; the reply echoes it.

```json
{"command":"seek","id":"7","seconds":12.5}
{"ok":true,"id":"7","status":{"recorder":"idle","lastRecordingPath":null,"playerPath":"C:\\v\\a.mp4","playing":false,"positionSeconds":12.5,"encoding":false}}
```

`enqueue-shrink` accepts only the quick targets offered by the shell menu
(`ShellIntegration.AcceptedShrinkTargetsMegabytes`); the request is handed to a new process with
`--kucult <MB> <path>`, the same path the shell menu takes.

## Reply

| Field    | Meaning                                                            |
|----------|--------------------------------------------------------------------|
| `ok`     | `true` when the command ran.                                       |
| `id`     | The request's `id`, if any.                                        |
| `error`  | Error code when `ok` is `false`.                                   |
| `detail` | Optional extra word (for example `no-media`, `NoPath`).            |
| `status` | Status object, on success of `status`, player and record commands. |

Status object: `recorder` (`idle`, `countdown`, `running`, `paused`), `lastRecordingPath`, `playerPath`,
`playing`, `positionSeconds`, `encoding`.

## Error codes

| Code               | When                                                                 |
|--------------------|----------------------------------------------------------------------|
| `disabled`         | The channel was not enabled with the start flag.                     |
| `malformed`        | The line is not a JSON object or has no `command`.                   |
| `unknown-command`  | `command` is not in the table above.                                 |
| `missing-argument` | A required field is missing.                                         |
| `bad-argument`     | A field has an invalid value (negative seconds, missing file, target not offered). |
| `not-ready`        | The window is not up yet, no media is loaded, or no recording runs.  |
| `busy`             | A recording or countdown is already running, or the app refused the file. |
| `failed`           | The command ran and failed; `detail` carries the app's message.      |
| `timeout`          | The app did not answer within 15 seconds.                            |
| `unreachable`      | Client side: no VidShrink owns the pipe.                             |
| `unsupported`      | Client side: the running VidShrink is older and does not know commands. |

## Command line client

```
vidshrink denetim kayit-baslat | kayit-durdur | durum [--json]
vidshrink control record-start | record-stop | status [--json]
```

Exit code 0 on success, 1 when the app refuses (with the reason on stderr), 64 on a usage error.
`--json` prints the raw reply line.
