# WebGL optimization audit — 2026-07-28

## Result

Completed the first production-safe optimization pass for WebGL runtime, build size and online transport.

- Final audit build: `Succeeded`, `0 errors`, `4 warnings`, `27,313,003` bytes for the whole output folder.
- The final build was opened from a Brotli-aware local server and reached the playable scene. UI, tutorial, water, materials and world rendering were visually correct.
- Browser lobby transport is no longer forced to one-second HTTP polling: WebGL now has a native JavaScript WebSocket bridge and keeps HTTP as an automatic fallback.
- Empty remote bases are dormant by default instead of activating full `SceneAny` clones.
- Google Sheets credentials and Google API assemblies are editor-only and are absent from the player.
- The tutorial hand texture uses a 512px WebGL import instead of a 1254px RGBA runtime texture.
- Unused Film Grain textures and inactive Bloom profile data were removed from the WebGL path.
- A tested nginx patch adds JSON gzip, upstream keep-alive and a dedicated WebSocket proxy location. It has not been deployed to production yet.

## Baselines

### Runtime / FPS

Editor profile before this pass:

- `PlayerLoop` median: `7.71 ms`;
- `PlayerLoop` p90: `9.98 ms`;
- approximately `49,958` GameObjects in the loaded scene, `4,017` enabled;
- `494` draw calls and approximately `122k` triangles in the measured view.

The largest structural cost is `RemoteBasesApplier`:

- one `SceneAny` base expands to about `6,183` GameObjects, `890` Canvases and `1,552` TMP components;
- six serialized remote slots account for roughly `37k` GameObjects before spawned gameplay objects;
- a player build loads about `396k` objects while serializing the main scene.

The current pass keeps empty slots inactive, which removes their active scripts/renderers/canvases from frame work. It does not remove their serialized hierarchy from startup or build data.

### Build size

Previous public build (`Zoo[26]-mirraSDK[5.1.20].zip`):

- zip: `24,862,624` bytes;
- data: `16,592,372` bytes;
- WASM: `8,139,207` bytes.

Final audit build (`Builds/OptimizationAudit/WebGL-NoFilmGrain`):

- whole folder: `27,313,003` bytes;
- four core Build files: `27,209,629` bytes;
- data: `18,955,529` bytes;
- WASM: `8,041,471` bytes;
- framework: `94,736` bytes;
- loader: `117,893` bytes.

The apparent growth is asset drift, not an optimization regression: the new 88-clip GDC 2026 audio collection adds `2,376,042` bytes of already-compressed Ogg data after the previous public build. Normalized for those new audio files, the core build is `110,621` bytes smaller than the comparable previous core files.

Additional memory/packed-data wins:

- tutorial hand: `6,295,440 -> 263,716` packed bytes (`-6,031,724`, about `-95.8%`) before final Brotli transport compression;
- WASM: `-97,736` bytes;
- Google credentials: removed from player;
- Google API assemblies: removed from player;
- Film Grain source textures: removed from the runtime resource list. Their reported packed size was `2,622,772` bytes, but they were highly Brotli-compressible, so the real transferred data improvement was only `6,912` bytes.

### Network / server

Production read-only checks:

- steady `/health` HTTPS TTFB: approximately `36–57 ms`;
- cold TLS request observed: approximately `453 ms`;
- API container: about `0.02%` CPU and `92.8 MiB` RAM during the check;
- host load was negligible and about `214 MiB` RAM remained available;
- deployed backend commit matched local backend commit `2a7011e`;
- existing WebSocket soak results remain healthy: ACK p95 `2.17 ms`, full broadcast p95 `8.30 ms`.

The main latency problem was client-side. WebGL explicitly reported WebSocket as unsupported and used:

- lobby update every `1 s`;
- lobby state poll every `1 s`;
- friend and gift inbox poll every `2 s`.

After this pass:

- WebGL uses the browser `WebSocket` API through `ZooWebSocket.jslib`;
- movement/update cadence is `0.2 s` (`5 Hz`);
- HTTP polling remains active until `ws_ready` and resumes on connection failure;
- idle friend/gift polling is `8 s`, while pending inbox polling remains `2 s`.

The browser-native bridge, generated IL2CPP externs and JavaScript merge were verified in the built player. A localhost Mirra session could not complete the live join because it supplied no player profile/body; production logs showed the expected `400` response in `1–2 ms`. This was not a CORS failure. A real platform two-client WebSocket smoke is still required.

## Implemented changes

- `LobbyClient.cs`
  - browser-native WebSocket interop;
  - stable callback receiver;
  - preserved JS callback methods;
  - shared inbox/heartbeat/reconnect/fallback path;
  - 5Hz WebSocket update cadence and shorter position sample window.
- `Assets/Plugins/WebGL/ZooWebSocket.jslib`
  - connect/send/close implementation using the browser WebSocket API.
- `FriendRequestInboxUI.cs`, `GiftInboxUI.cs`
  - slower polling while idle.
- `RemoteBasesApplier.cs`, `RemoteBasesApplier.prefab`
  - empty full-base clones remain disabled;
  - randomized empty showcase farms disabled for production.
- WebGL texture and URP settings
  - tutorial pointer limited to 512px;
  - unused post-processing variant stripping enabled;
  - inactive Bloom removed;
  - minimal default volume profile;
  - unused Film Grain texture references cleared.
- Localization tooling
  - Google Sheets code, assemblies and credentials moved to editor-only scope;
  - credentials metadata added to `.gitignore`.
- Legacy Resources cleanup
  - unused `DamagePopup` and obsolete Mirra editor reference assets moved out of `Resources`.
- Backend nginx configuration (`E:\GitFork\zoogame-backend\nginx\conf.d\default.conf`)
  - gzip for JSON responses larger than 1 KiB;
  - reusable upstream HTTP connections;
  - dedicated unbuffered `/ws/lobby` proxy.

The nginx configuration passed `nginx -t` in an isolated container on the production host. A real `/zoo/locations` response measured `11,234` raw bytes and `2,386` gzip bytes (`-78.8%`). Production was not restarted or modified.

## Verification

- Unity 6000.3.9f1 compilation: successful.
- Final WebGL build: `Succeeded`, `0 errors`.
- Final browser visual smoke: successful.
- WebSocket JavaScript included in generated build: verified.
- Google API assemblies and credentials absent from generated player: verified.
- Server health, container state and recent request logs: checked read-only.
- nginx candidate config: syntax test successful.

The four warnings in the last full BuildReport were all obsolete assets under `Resources`:

- `PrototypeReferences`;
- `UIElementsReferences`;
- two missing scripts on unused `DamagePopup`.

All three assets were moved out of `Resources` after the final build. The move is imported and compile-safe, but a fourth eight-minute player build was intentionally not run solely to regenerate the warning count.

## Remaining priorities

1. Replace six serialized `SceneAny` remote bases with one lightweight remote proxy/lazy instance path. This is the biggest remaining startup, memory and weak-device FPS opportunity.
2. Run two real platform WebGL clients for WebSocket movement, reconnect and HTTP fallback acceptance.
3. Deploy the tested nginx config with a backup, then remeasure compressed responses and WebSocket upgrade.
4. Produce a Development WebGL build with stack traces for the eleven startup messages `Cannot instantiate objects with a parent which is persistent`.
5. Spread or lazy-load the new audio catalog if weak-device profiling confirms that the initial Ogg decode burst affects time-to-interactive.
6. Profile on a low-end target browser/GPU; add remote-base LOD/proxies and remove nonessential remote colliders/listeners based on measured cost.

