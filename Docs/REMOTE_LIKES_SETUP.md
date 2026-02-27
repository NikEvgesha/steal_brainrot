# Remote Likes Setup

Updated: 2026-02-27

This document describes how to configure and test remote player likes in Unity client (`steal_brainrot`).

## 1) What is implemented

- Like button in remote profile popup (`RemoteProfilePopup`).
- Likes counter for target player.
- Limit: one like per target player per day (server-side rule).
- If player tries to like again on same day:
  - temporary popup notice appears,
  - repeated presses do not stack notices; timer is refreshed.

## 2) Runtime flow

1. Open remote player board -> open profile popup.
2. Client requests `/likes/state` for selected target.
3. UI shows current likes count and like availability.
4. Press `Like`:
   - success -> counter updates, button becomes disabled for today,
   - duplicate same-day like -> notice appears for a few seconds.

## 3) Scene/prefab requirements

No extra prefab wiring is required.

Used scripts:
- `Assets/_Scripts/Net/RemoteFriendBoard.cs`
- `Assets/_Scripts/Net/FriendsApi.cs`

Requirements:
- `G.Backend.FriendsApi` must be available in runtime.
- Remote profile board must provide `playerId` and/or `friendCode` (already done through `SetRemoteWithId`).

## 4) Localization keys (optional but recommended)

Current code has fallback text, so feature works without keys.

Recommended keys to add:
- `UI/Profile/LikeButton`
- `UI/Profile/Likes`
- `UI/Profile/LikedToday`
- `UI/Profile/LikeAlreadyToday`
- `UI/Profile/LikeUnavailable`
- `UI/Common/Loading`

## 5) Smoke test

1. Start two clients and join same lobby.
2. Client A opens profile of Client B:
   - verify likes counter shown.
3. Press like once:
   - counter increments by 1,
   - button changes to "Liked today" state.
4. Press like repeatedly:
   - no extra increment,
   - temporary notice appears,
   - notice timer is refreshed on each extra press.
5. Restart client A and reopen profile of B:
   - state remains consistent with server (`likedToday=true`).

## 6) Known rule

Daily reset uses server UTC day boundary.
