# Edit Mode test suite

Run from Unity: **Window > General > Test Runner > EditMode > Run All**.

This suite has **never been executed** — the sandbox the feature was built in has no Unity Editor.
It compiles cleanly against `Tools/FullTypecheck`, but that only checks types. Please treat the
first run as part of the review.

## Property coverage

Every property from the design doc has at least one test. Property tests run
`Property.Iterations` (200) seeds and print the failing seed plus an operation log.

| Property | Test |
| --- | --- |
| 1. Coin conservation and non-negativity | `MetaSystemTests.Property01_...` |
| 2. Owned never exceeds cap | `MetaSystemTests.Property02_...` |
| 3. Installed never exceeds owned | `MetaSystemTests.Property03_...`, `InstallTests` |
| 4. Install slots are mutually exclusive | `InstallTests.Property04And05_...` |
| 5. Boosters do not stack | `InstallTests.Property04And05_...`, `SpeedChannelRegressionTests` |
| 6. Mission progress is monotonic | `MissionTests.Property06_...` |
| 7. First-clear bonus paid exactly once | `MissionTests.Property07_...` |
| 8. Profile round trip / corrupt input | `PersistenceTests.Property08_...` (x2) |
| 9. Schedule determinism | `SpawnScheduleTests.Property09_...`, `HeadlessReplayTests` |
| 10. Blind ratio inside binomial bound | `SpawnScheduleTests.Property10_...` |
| 11. Revealing is one-way | `RevealTests.Property11_...` + a static source guard |
| 12. Gates never produce jams | `GateJamRegressionTests` (5 tests), `HeadlessReplayTests` |
| 13. Speed channels are independent | `SpeedChannelRegressionTests` (6 tests) |
| 14. Settlement consistency | `MissionTests.Property14_...` |
| 15. Unlock iff every mission cleared | `MissionTests.Property15_...` |

## The two defect regressions

`GateJamRegressionTests` and `SpeedChannelRegressionTests` guard the two pre-existing bugs fixed
by this change. Both were written to fail against the old code:

- **Gate/jam.** Before the fix, `TrafficSystem` accumulated `StallTime` for any barely-moving
  parcel, so a gate's 8 second hold sailed past the 3 second jam threshold. Closing a gate
  reliably produced the jams the gate exists to prevent. `SpeedChannelRegressionTests` would not
  even have compiled before the fix, because `BeltPath.DeviceSpeedBonus` did not exist.
- **Speed channels.** `BeltPauseSwitch` and `BeltSpeedToggle` overwrite `SpeedMultiplier`
  outright, so a Booster writing to the same field would have been erased by a pause tap.

`GenuineBlockageStillCountsAsAJam` and `OneParcelIsCountedAsAJamOnlyOnce` keep the gate exemption
narrow, so the fix cannot hide a real deadlock.

## Fixtures

| File | Purpose |
| --- | --- |
| `PropertyRunner.cs` | Seed-driven property runner with an operation log for failures |
| `LevelFixtures.cs` | Loads the shipped `level_01.json`, so tests assert against real data |
| `MiniYard.cs` | Builds belts/nodes/gates/scanners without `LevelLoader`, prefabs or a scene |
| `HeadlessRoundSim.cs` | Replays a whole round at `dt = 1/60`: schedule -> emit -> traffic -> mission |

`HeadlessRoundSim` is what turns balance prose into measurements: it is used to check that M1 is
winnable bare handed, that no mission deadlocks, and that two Scanners cover M3's entire blind load.
