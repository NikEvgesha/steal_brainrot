# Unity MCP wrapper (for Codex workflow)

This project uses Unity Bridge over HTTP on `http://localhost:7777`.

Use this wrapper script for repeatable checks and calls:

- `Tools/unity-bridge/mcp-wrapper.ps1`

## Preconditions

1. Unity project is open.
2. `Window -> Unity Bridge` is open.
3. Bridge server is running (`HTTP Server started on port 7777`).

## Quick checks

From project root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\unity-bridge\mcp-wrapper.ps1 -Command ping
```

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\unity-bridge\mcp-wrapper.ps1 -Command scene_hierarchy
```

Or run the bundled smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\unity-bridge\mcp-smoke.ps1
```

## Common commands

### Scene grep

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\unity-bridge\mcp-wrapper.ps1 -Command scene_grep -Query "name~'Player'"
```

### Game screenshot

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\unity-bridge\mcp-wrapper.ps1 -Command screenshot -OutFile ".\Tools\unity-bridge\game.png"
```

### Camera screenshot

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\unity-bridge\mcp-wrapper.ps1 -Command camera_screenshot -Position 0,2,-5 -Target 0,0,0 -Fov 60 -OutFile ".\Tools\unity-bridge\cam.png"
```

### Execute C# in Editor

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\unity-bridge\mcp-wrapper.ps1 -Command execute -Code "return UnityEngine.Application.unityVersion;"
```

## Raw request mode

Use when endpoint/payload differs:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\unity-bridge\mcp-wrapper.ps1 -Command raw -Endpoint "/api/scene_hierarchy" -Json "{}"
```

## Notes

- `ping` to `/` may return `"Unknown endpoint: /"` with HTTP 200. This still means server is reachable.
- Warnings in `UnityOperations.*` about obsolete APIs are non-blocking for wrapper usage.
- If calls fail, check Unity Console first, then rerun `ping`.
