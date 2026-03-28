# Album + Unity Bridge MCP mini handoff (2026-03-28)

## 1) Current branch state
- Branch: `develop`
- Remote tracking: `develop...origin/develop`
- Last pushed commit: `515722c` (`Album: stabilize rarity state and add save tools`)
- There are local uncommitted changes.

## 2) What is already done (from commit history)
- Stabilized rare tab flow and card list behavior.
- Added/updated save reset tools (`Save/Clear Album Saves`, then extended toward full save reset flow).
- Split egg and animal detail presentation (foundation for different right-side info panels).
- Fixed issues around list disappearing, wrong rarity switching, and reward mentions.

Main commits:
- `515722c` Album: stabilize rarity state and add save tools
- `188dc6e` Album: split egg and animal info presentation
- `d55ea2c` Finalize album rare tab selection and include workspace updates
- `f1f4289` Fix album rare tab UX and reward button visibility
- `0fbae0b` Fix album grid rebuild stability and sync updated album UI

## 3) Local changes right now (not committed yet)
- `Assets/_Scripts/UI/Album/AlbumProgressService.cs`
- `Assets/_Scripts/UI/Album/AlbumScreenController.cs`
- `Tools/unity-bridge/unity-extension/Editor/HttpServer.cs`
- `Tools/unity-bridge/unity-extension/Editor/UnityOperations.Compiler.cs`
- Editor noise files:
  - `Packages/packages-lock.json`
  - `Assets/Resources/MirraSDK5/PackageManagerInspector.asset`
  - `Assets/Resources/MirraSDK5/PackageManagerInspector.asset.meta`

## 4) Functional status of local fixes
### Album
- Added `RareType` fallback resolution using prefab data when incoming/event rarity is invalid.
- Reworked entry collection so:
  - duplicates for the same entity id are merged,
  - default card/icon prefers base rarity variant (normally `Common`),
  - rare mentions are handled across supported rarities.
- List filtering now keys off discovered entity state, so rare switching does not collapse the list.

### Unity Bridge
- In `HttpServer`: removed aggressive thread abort stop path, added safer stop/dispose handling.
- In `UnityOperations.Compiler`: improved compiler/reference resolution.
- Key result: `/api/execute` path now works for `return 1+1;` (previously failed on `System.Text.Encoding.CodePages`).

## 5) Quick verification checklist
1. Open Unity project.
2. Open Unity Bridge MCP window, confirm `Status: RUNNING`, port `7777`.
3. In bridge window run:
   - `Test Scene Hierarchy` -> should show `SUCCESS`
   - `Test Execute (return 2)` -> should return successful result
4. Album checks:
   - Open egg/animal tabs and verify active tab visual state.
   - Select an entry -> right panel should apply selected rarity correctly.
   - Switch rarities -> list should not disappear/break; album card should keep base icon behavior.

Optional CLI check for execute endpoint:
```powershell
$body = '{"code":"return 1+1;"}'
Invoke-RestMethod -Uri "http://localhost:7777/api/execute" `
  -Method Post -ContentType "application/json" -Body $body
```

## 6) First steps for the next chat
1. Separate functional changes from editor noise.
2. Create clean commit(s):
   - Album fixes
   - Unity Bridge fixes
3. Run checklist from section 5.
4. After user/QA confirmation, push to `origin/develop`.

## 7) Risk note
- `packages-lock.json` and `PackageManagerInspector.asset*` are likely editor side effects.
- Do not mix them into functional commits unless explicitly required.
