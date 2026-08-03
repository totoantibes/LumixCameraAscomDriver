# Handover to the N.I.N.A. team — two ASCOM camera issues

**From:** Lumix ASCOM Camera driver (`ASCOM.Lumix.Camera`, v8.0.0) — https://github.com/totoantibes/LumixCameraAscomDriver
**Repo this concerns:** https://github.com/isbeorn/nina (issues previously at `bitbucket.org/Isbeorn/nina`)
**Test rig:** Panasonic GH5S, over both Wi-Fi and USB (PTP SDK), ASCOM Platform 6.6, Windows 11.

Both issues are **client-side**. The driver passes ConformU with no errors, and SharpCap
renders the same driver correctly in both cases. Neither can be worked around in the
driver — that is why they are being raised here rather than fixed on our side.

Line references are against `isbeorn/nina` as read on 2026-08-04.

---

## Issue 1 — ASCOM `Gains` names are discarded, so the user sees `0…N-1` instead of ISO values

### What the user sees

The camera's gain dropdown lists `0, 1, 2, … 17`. The actual ISO values (`100, 200, 400,
… 51200`) appear nowhere in the UI. Selecting the right ISO means counting positions, or
reading `Gain 7 Mapped to 800` out of the NINA log.

This was reported before as **Bitbucket issue #1150**. What shipped in response was the
log line, not the display — the names are still dropped on the floor.

### What the ASCOM spec says

From the canonical camera definition
(https://ascom-standards.org/newdocs/camera.html):

> **Gain** — "Gets or sets the camera's gain (ISO). Expressed as an integer index into the Gains array."
>
> **Gains** — "Returns a list of available gain names as strings."
>
> "If Gains is not empty then GainMin and GainMax must both be unavailable (throw
> PropertyNotImplementedException). If Gains is empty, then Gain must return an integer
> between GainMin and GainMax."

So a driver picks exactly one of two modes. Ours must use **index mode**, and this is not
a preference:

* `ICameraV2.Gain` is a **`short`**. Any ISO above **32767** cannot be represented in
  value mode at all. Lumix bodies advertise ISO values well past that.
* Lumix ISO steps are a sparse, camera-reported list, not a range — value mode would
  invite clients to send values the body rejects.

Our implementation (`LumixCamera/Driver.vb`):

| member | behaviour |
|---|---|
| `Gains` | `ArrayList` of **`String`**: `"100"`, `"200"`, … (from the camera's own capability list over USB, the ISO table over Wi-Fi) |
| `Gain` | index into that list, get and set |
| `GainMin` / `GainMax` | `throw PropertyNotImplementedException` — required, since `Gains` is non-empty |

### Root cause in NINA

`NINA.Equipment/Equipment/MyCamera/AscomCamera.cs`, in the connect-time init (~line 68):

```csharp
Gains.Clear();
try {
    var gains = device.Gains;
    int idx = 0;
    foreach (object o in device.Gains) {
        if (o is string) {
            // Per the ASCOM spec, if we have Gains, then they are names, not values.
            // Add an index for each value and write the mapping to the log.
            // TODO - Look at how to carry the names to the UI
            // eg by adding a GainsPreset string list to ICamera.
            // Making Gains a string has too many ripple effects with the
            // UI for a quick fix.
            Logger.Info($"Gain {idx} Mapped to {o as string}");
            Gains.Add(idx++);          // <-- the name is read and thrown away
        }
    }
} catch (Exception) {
}
```

The name never leaves this method. It cannot, because the receiving collection is typed
`IList<int>` (same file, ~line 447):

```csharp
private IList<int> _gains;
public IList<int> Gains { get { if (_gains == null) { _gains = new List<int>(); } return _gains; } }
```

The in-code `TODO` is an accurate statement of the problem; this report is mostly a
request to promote it, plus the observation below that the fix may be smaller than the
comment assumes.

### Suggested fixes

**Option A — carry the names (what the TODO proposes; our recommendation).**
Add a parallel `IList<string>` (`GainNames` / `GainsPreset`) to `ICamera`, populate it in
the loop above, and bind the gain selector's `DisplayMemberPath` to it while the bound
value stays the `int`. Nothing about the persisted meaning of "gain" changes, so profiles,
sequence items and the flat wizard are untouched. Cameras that expose no names fall back
to today's display.

**Option B — a two-line change, with a caveat worth weighing.**
When *every* entry in `device.Gains` parses as an integer, store the parsed value instead
of the index:

```csharp
Gains.Add(int.Parse(o as string));   // instead of Gains.Add(idx++)
```

The surrounding code already handles a non-identity list correctly, which is easy to
miss — no other edit is needed:

* `Gain` get: `val = (int)Gains[device.Gain];` — indexes by the driver's index, returns the ISO. ✔
* `Gain` set: `short idx = (short)Gains.IndexOf(value); device.Gain = idx;` — maps the ISO back to the index. ✔
* `GainMin`/`GainMax`: already fall back to `Gains.Aggregate(min/max)` when the driver throws → `100` / `51200` instead of `0` / `17`. ✔

**The caveat:** this silently changes what a stored gain number *means*. Existing profiles,
sequence templates and flat-wizard settings holding `7` would start meaning ISO 7 rather
than the 8th ISO. It also needs a guard for non-numeric names (`"HCG"`, `"Low"`) and for
duplicate entries, which would break `IndexOf`. If you take this route it needs a profile
migration; if that is unwelcome, Option A is the clean one.

### Verification

Same driver, same camera, SharpCap displays the ISO strings from `Gains` correctly — the
values are on the wire and reach the client.

---

## Issue 2 — the ASCOM setup dialog is unreachable whenever the device is connected

### What the user sees

Once the camera is connected, NINA's setup/properties button for the ASCOM device is
disabled. Changing anything the driver owns — transfer format (RAW/JPG/Thumb), default
ISO and shutter, temp folder, or opening the driver's Live View window — requires
disconnecting the camera and reconnecting. Mid-session that is disruptive; APT allowed
this.

### Root cause in NINA

`NINA.Equipment/Equipment/AscomDevice.cs:70`:

```csharp
public bool HasSetupDialog => !ShouldBeConnected;
```

and the method re-checks the same flag (~line 334):

```csharp
public void SetupDialog() {
    if (HasSetupDialog) {
        ...
```

This is a blanket policy across **every** ASCOM device type, not just cameras — no driver
can offer setup while connected, regardless of whether it handles it correctly.

### What the spec says

The canonical definition places no restriction on the connected state:

> "Launches a configuration dialogue box for the driver. The call will not return until
> the user clicks OK or cancels manually. Please note that this method is only valid for
> COM drivers. Alpaca devices should provide configuration through the Alpaca HTML
> endpoints and should not implement a SetupDialog endpoint."

So this is a NINA policy choice, not a conformance requirement.

### The reason the policy exists is legitimate — the ask is narrower

A blanket guard is a reasonable default: plenty of drivers put connection-critical
settings (COM port, IP address, sensor geometry) in the setup dialog and would misbehave
if edited live. The request is not to remove the guard, but to let a driver that handles
it opt in.

For reference, this is the contract we implemented on our side. While `Connected` is true,
our setup dialog:

* **disables** the fields that define the connection and cannot change without a
  reconnect — camera IP, transport (Wi-Fi / USB), and sensor resolution (which clients
  read once and cache) — and says so in the title bar;
* **applies everything else to the live camera immediately** on OK — ISO, shutter,
  transfer format, temp folder;
* never re-opens or drops the existing connection as a side effect of being shown.

### Suggested fixes

**Option A (recommended) — opt-in, default-off.**
Keep `HasSetupDialog => !ShouldBeConnected` as the default, and add a per-device equipment
setting ("Allow driver setup while connected"). Behaviour is unchanged for every existing
user and driver; drivers that support it become usable without a reconnect. A one-line
change to the expression plus a profile flag.

**Option B — let the driver decide.**
Drop the connection term entirely. Drivers already know their own `Connected` state and
can disable what they must. Correct per the spec, but it exposes users to legacy drivers
that never expected it — hence the preference for A.

Whichever way this goes, `RaisePropertyChanged(nameof(HasSetupDialog))` is already wired
to the connection state (`AscomDevice.cs:183`), so the button re-enables without extra
plumbing.

---

## Summary

| # | Issue | NINA file | Spec-mandated? | Suggested change |
|---|---|---|---|---|
| 1 | `Gains` names discarded → indices shown | `NINA.Equipment/Equipment/MyCamera/AscomCamera.cs` (~68, ~447) | Yes — `Gains` is defined as names | Carry names to the UI alongside the int index |
| 2 | Setup dialog blocked while connected | `NINA.Equipment/Equipment/AscomDevice.cs:70` | No — NINA policy | Per-device opt-in, default off |

Issue 1 is the one that actually costs users: with a DSLR-class driver the gain control is
currently unusable without cross-referencing the log, and no driver-side change can fix it,
because index mode is the only legal mode when ISO exceeds a 16-bit signed short.

Happy to test a build against the GH5S over either transport.
