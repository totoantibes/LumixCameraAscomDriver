# Changelog

All notable changes to the Lumix ASCOM Camera driver are documented here.

## v8.2.0 — 2026-08-13

Reliable RAW-over-Wi-Fi, fully in-memory decoding, honest exposure reporting, a choice of
sub-second exposure handling, and a simpler setup dialog.

### Fixed
- **RAW capture over Wi-Fi now completes reliably.** The camera streams an ~18 MB RW2 slowly,
  in one chunked response with no `Content-Length`; the old readout aborted at a fixed 30 s
  wall clock and passed on a truncated frame. The Wi-Fi download now **resumes until the whole
  file has arrived** — a byte-range resume if the camera stalls mid-stream, an overall ceiling,
  and a no-progress bailout instead of the flat timeout.
- **Wi-Fi content browse hardened against the camera's DLNA quirks.** The driver indexed the
  DLNA `Browse` by `cam.cgi`'s file count, but the two disagree whenever the card holds files
  the camera cannot serve (e.g. foreign RAW such as Sony `.ARW`), pushing the index past the
  DLNA range and crashing with a `NullReferenceException`. It now **re-aims off the DLNA
  server's own `TotalMatches`**, retries through the transient `HTTP 500` / UPnP `701`
  flapping while the camera reindexes, and raises a clear, diagnosable error instead of an NRE
  when there is genuinely nothing to read.
- **`LastExposureDuration` now reports the exposure actually taken.** On the USB one-shot path a
  requested time is snapped to the nearest discrete shutter speed; the driver logged the snapped
  value but still reported the *requested* one, so a client's exposure/ADU model (e.g. NINA's
  flat-wizard bisection) broke when several requested values snapped to the same speed. It now
  reports the real duration.
- **Setup dialog no longer freezes on Live View close.** Closing Live View sends `stopstream`
  synchronously on the UI thread; with the default 100 s HTTP timeout an unresponsive camera
  froze the dialog. That call now uses a short (1.5 s) timeout.

### Added
- **Sub-second exposure mode (USB Extended).** A setup-dialog choice for exposures under 1 s:
  *Camera list* snaps to the nearest real shutter speed (accurate, discrete), or *Bulb* holds
  the shutter open for the exact requested time (no snapping, but very short bulbs are
  imprecise). Only USB Extended can honour it — Wi-Fi is always bulb and USB Standard can only
  snap — so the control is greyed with a tooltip explaining why. Default: Camera list.

### Changed
- **RAW decodes entirely in memory on both transports** (Wi-Fi and USB): the transfer buffer
  goes straight to LibRaw (`libraw_open_buffer` → `libraw_dcraw_make_mem_image`), removing an
  ~18 MB write-plus-read per frame. JPG is decoded from the buffer and only its one
  intermediate TIFF touches disk — in the **system** temp area under a unique name, deleted as
  soon as `ImageArray` is read.
- Faster, leaner readout: `ImageArrayVariant` is built with `Array.Copy` and cached; `PLAYMODE`
  is no longer sent three times per readout; BULB is no longer re-armed on every exposure;
  per-phase Wi-Fi readout timings were added to the trace.
- LibRaw (64-bit) is **0.22.2**.

### Removed
- **The "Temp folder" setting and its field in the setup dialog.** Nothing is staged to a
  user-configured folder any more, so the option only invited confusion.

### Upgrade note
- `AssemblyVersion` moved 8.0.x → **8.2.0**, which changes the COM binding. Run the installer
  (or re-register the DLL) so ASCOM clients load the new build.

## v8.0.0

- USB transport (Standard + Tether SDK), including bulb exposures over 60 s via LUMIX Tether's
  SDK (SDK only; Tether itself is never launched or redistributed).
- Live View window on both Wi-Fi and USB.
