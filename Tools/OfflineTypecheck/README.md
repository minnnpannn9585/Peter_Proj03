# Offline verification harnesses

This feature was implemented in a sandbox with **no Unity Editor**: no `Library/`, no
`UnityEngine.dll`, no `Unity` on `PATH`. The real project cannot be compiled or played there.
These two projects are the closest achievable substitute, and they are the reason the change can
be reviewed with any confidence at all.

Neither is part of the Unity build: `Tools/` sits outside `Assets/`, so Unity never compiles them.

## 1. `Tools/FullTypecheck` — does it compile?

```
dotnet build Tools/FullTypecheck
```

Type-checks **every** file in `Assets/Scripts` and `Assets/Tests`, MonoBehaviour and test code
included, against hand written API-shaped stubs (`FullUnityShims.cs`, `TestFrameworkShims.cs`).
Nothing is executed. It catches typos, wrong overloads, missing members, and switches that no
longer cover an enum.

Currently: **0 errors, 0 warnings.**

## 2. `Tools/OfflineTypecheck` — does the logic behave?

```
dotnet run --project Tools/OfflineTypecheck            # add --verbose to list every check
```

Compiles the non-MonoBehaviour half of the project and **runs real assertions** against it.

| Check | What it proves |
| --- | --- |
| `WalletChecks` | Property 1: coin conservation, non-negative balance, `Earn` guard |
| `InventoryChecks` | Property 3: `Free == Owned - Installed` over random interleavings |
| `ShopChecks` | Property 2: cap respected; every `ShopError` branch; buys are atomic |
| `JsonWriterChecks` | Escaping and exact float round-trip through `MiniJson` |
| `ProfileChecks` | Property 8: round trip, plus missing/truncated/corrupt/version-mismatch inputs |
| `MissionRuntimeChecks` | Properties 6, 7, 14: verdict order, `-1` jam cap, idempotent `Resolve` |
| `SpawnScheduleChecks` | Properties 9, 10: determinism, `Planned` = 18/56/108, blind binomial bound |
| `LevelChecks` | `level_01.json` topology, 63 + 3 = 66 slots, reachability, device catalog |
| `BalanceChecks` | The 20 → 240 → 60 growth curve, read from JSON not hard coded |
| `FeasibilityChecks` | Each mission's time limit against a queue-free lower bound; difficulty gradient |

Currently: **292 checks, 0 failures.**

## What these do NOT prove

A green result here says nothing about:

- whether the project compiles under **Unity 6000.3.18f1** (these are stubs, not the engine);
- whether the **Unity Test Runner** passes — `Assets/Tests/EditMode` is the real suite and has
  never been executed in this sandbox;
- whether `SampleScene.unity` opens with no missing references;
- any **runtime** behaviour of MonoBehaviour code: belt movement, gate timing, install
  raycasting, or a single pixel of the HUD.

In particular, these design claims are **asserted in the Edit Mode suite but unverified here**:

- M1 is winnable with no devices bought (`HeadlessReplayTests`);
- M2's designed failure mode is the clock — this depends on merge queueing and human reaction
  speed, which no static model captures;
- two Scanners reveal M3's entire blind load (`HeadlessReplayTests`);
- the gate/jam and speed-channel fixes behave correctly in a running yard
  (`GateJamRegressionTests`, `SpeedChannelRegressionTests`).

Please run `Window > General > Test Runner > EditMode` in Unity to close those gaps.
