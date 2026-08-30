# Parcel Sort — 3D Express Sorting Game

**This README is the full design handoff.** Future agents must work from this file alone. Do not ask the user to re-paste the original chat. If a feature is listed here, it is in scope.

A 3D express-parcel sorting game. Packages enter from one side as a continuous stream (large and small), travel **left → right** on a tangled conveyor maze, and must reach the **color-tagged truck** on the right. The player does not carry packages. They stand in front of a system that never stops and **keep reconfiguring the sorting routes**.

Art direction: **low-poly** (original note: “lopoly”). Current builds use **cubes / primitives only**. Art will be replaced later. Gameplay must stay prefab- and data-driven so meshes can swap without rewriting systems.

Engine: **Unity 6000.3.18f1** · Render pipeline: **URP** · Scene: `Assets/Scenes/SampleScene.unity`

---

## Working Principles

These rules apply to **every** development round. If a change fights a principle, change the approach, not the principle.

### P1 — Language split

| Channel | Language |
| --- | --- |
| Chat with the user (replies, plans, questions, status) | **简体中文** |
| Agent internal workflow (reasoning, tool plans, implementation notes) | **English** |
| In-game UI (HUD, buttons, events, score cards, tutorial copy, world labels) | **English** |
| Code, prefab names, JSON keys, comments, commit messages | **English** |

Do not put Chinese strings in runtime UI. Design examples that used Chinese tags (e.g. `【蓝色】【冷藏】【加急】`) ship as English: `[Blue] [Chilled] [Urgent]`.

### P2 — Levels are data, not code

A new level must be possible **without writing new C#**.

- One shared yard scene + a **LevelLoader**.
- Layout is assembled at runtime from **reusable modules** (inlet, belt, corner, junction, scan slot, truck bay, warehouse slot, …).
- Each level is a **JSON config** (plus any referenced module ids). Adding `level_06.json` is how you add level 6.
- Spawn rates, truck schedules, events, upgrade slots, and module transforms all come from that file.
- **Forbidden:** `if (levelId == 3) { ... }`, unique per-level MonoBehaviours, or hand-placing a whole yard in a scene that cannot be reproduced from JSON.

If a feature cannot be expressed in the level JSON + module catalog, it is not a level feature yet — extend the schema or the module set, then author the JSON.

---

## Canonical Design Brief

Everything the user specified at kickoff, kept here so nothing depends on chat history. Later sections paraphrase into systems and phases; **this section is the source of truth for intent and examples.** In-game copy is English (P1). Original Chinese examples are preserved next to the English UI so meaning is not lost.

### What the game is

This is a **3D express sorting game**. Parcels come out together from one side. There are **large and small** parcels. They travel **from left to right** along conveyors.

- **Left:** inbound side. Depending on difficulty there are **3 to 6 inlets**, sending parcels in a **continuous stream**.
- **Middle:** a **tangled / complex** conveyor network. Some switches work like **train turnouts**: a **2-choose-1** path split. Other switches can **pause a conveyor**.
- **Right:** outbound side. Exits are **trucks keyed to parcel color labels** — not anonymous holes in the wall.
- **Player job:** operate those scene switches (and later placed equipment) so parcels reach the **correct truck**. The player is not a carrier.

### The feel (do not lose this)

Parcels keep running. Conveyors keep running. Sorting equipment works automatically once installed. Trucks leave on schedule. The player stands in front of this high-speed logistics system and **constantly reconfigures the sorting routes**. That is what should feel like “express sorting,” not a stop-and-carry puzzle.

### Art

- Target look: **low-poly**.
- **Now:** cubes / primitives only. No finished art assets yet.
- **Later:** replace placeholders. Do not block gameplay on art. Do not bake cube-only assumptions into code (use prefabs).

### How we build it

- Develop **step by step across multiple rounds**. One round cannot finish the whole game.
- Final product needs **at least five levels**, with a **significant difficulty change** between them (not only faster spawn).
- All upgrade tools (auto destination detection / scan, directional control, auto-divider / auto-sorter, warehouse, gates, speed belts, …) must be **actually implemented and usable in a real round**, not shop icons.
- Levels load from **JSON configs** (P2). Chat with the user in **简体中文**; agent workflow and in-game UI in **English** (P1).

### Feature 0 — Between-run upgrades + inspect-first

At the start of the player’s career, they must **manually click to inspect** a parcel to see its destination. They cannot know every parcel’s info up front.

Later they **buy more equipment** and, **before the round starts**, **manually install** it into the yard (physical placement, not a passive global buff). Examples that must exist as real devices:

| Device | What the player does with it | What it does in-round |
| --- | --- | --- |
| **Auto scanner** (barcode / scan machine) | Buy, then mount on a belt slot before the round | Parcels that pass it are scanned; destination (and tags) **show automatically** |
| **X-ray machine** | Buy, then mount on a belt slot | Passing parcels reveal **full hidden info** (destination + chilled + urgent + size). Distinct machine from the barcode scanner |
| **Auto-sorter** | Buy, then mount on a **junction / turnout** | That 2-way split becomes **automatic** (routes by scanned destination) |
| **Temporary warehouse** | Buy, then mount on a spur / slot | Can **store several parcels**, then the player dumps / sends them later when the line or truck is ready |

“Automatic delivery detection,” “direction control,” and “automatic divider” from the kickoff note map to **scanner / inspect**, **splitter direction**, and **auto-sorter**. All three must work in play.

### Feature 1 — Inlet gates

Inbound inlets have **gates**. The player can **temporarily close** a gate so parcels **stop coming in**, buying time to operate the yard. If the gate stays closed **too long**, it **auto-reopens**. This is pressure, not a pause menu.

### Feature 3 — Conveyor speed control

**Some** (not all) conveyor segments can be **clicked to switch to high speed**, then **clicked again to switch back** to normal. Toggle, not a hold.

### Feature 4 — Scan equipment (“information is also a resource”)

Especially suited to 3D. Put machines in the scene: **barcode scanner**, **X-ray**, **auto-sorter**.

A parcel shows **complete information only after it has been inspected or has passed a scan point**. Kickoff example (UI ships in English):

```
📦 Generic / unlabeled crate   (player does not yet know the real destination)
        ▼
      SCAN
        ▼
   [Blue] [Chilled] [Urgent]
```

Original illustration used a “red-looking box” that after scan was **Blue + chilled + urgent**. Preserve that beat: **appearance before scan is not the full truth.** Destination color and tags are hidden data. The player must not get a free perfect sort on unscanned parcels.

Design intent: **acquiring information is a resource.** The player cannot know all parcel info at the start of a run (or of their meta progression).

### Feature 8 — Trucks are not static exits

Trucks have **capacity**. Kickoff example (English UI):

```
🚚 Red truck
0 / 20
```

When full:

```
🚚 Depart
```

Then the **next truck of that color enters** the bay.

Trucks can also be **late**. Kickoff example:

```
Blue truck ETA: 30s
```

While the blue truck is absent, **blue parcels must not pile without limit** or the **entire system jams**.

Also: trucks **depart on a schedule**, not only when full. A bay can be empty. Wrong-color loading is a miss.

### Feature 10 — Random event system

Every round should produce **different problems**. Events are random (from a per-level table in JSON). Kickoff set — implement all of these; timings below are the canonical examples (tunable later, but do not drop the event type):

| Event | What happens | Example English banner |
| --- | --- | --- |
| Conveyor fault | A zone’s belt **stops** | `⚠ Conveyor fault — Zone C belt stopped 15s` |
| Truck arriving early | A truck’s departure is pulled forward | `⚠ Early truck — Red truck departs in 10s` |
| Express peak | Spawn count **×2** for a window | `⚠ Peak rush — parcel volume ×2 for 20s` |
| Label system failure | For a window, parcel **colors cannot be auto-identified** (scan/auto-sort degraded; inspect may still be the fallback) | `⚠ Label failure — colors unreadable 30s` |
| Oversized burst | **Many oversized / extra-large** parcels spawn next | `⚠ Oversize burst — incoming oversized parcels` |
| Temporary road closure | Maintenance **closes a zone** (e.g. Zone B unusable) | `⚠ Road closed — Zone B shut for maintenance` |

### Scoring — not only success / fail

Do **not** ship win/lose as the only result. Rank **S / A / B / C / D**. Players should **want to grind high scores**.

Score from **all** of these (weights tunable; none of the axes are optional to track):

| Axis | Meaning |
| --- | --- |
| Correct sort rate | Right truck vs wrong truck |
| Average handling time | How long parcels spend in the yard |
| Parcel damage rate | Crushed, timeout, mishandled |
| Jam count | System blockages |
| Truck load rate | How full trucks are when they leave |
| Equipment use count | How often placed equipment actually fired / was used |
| Energy | Cost of high-speed belts, extra scanners, etc. |

Kickoff end-card example (English UI, keep stars **and** letter rank **and** the numbers):

```
⭐⭐⭐⭐⭐
Rank S
Correct    99.8%
Avg delay  1.3s
Damage     0
Jams       0
```

### Kickoff coverage checklist (do not drop)

Use this when a new agent starts. Every row must remain true of this README and of the eventual game.

| # | Kickoff item | Captured |
| --- | --- | --- |
| — | 3D, left in / maze middle / right color trucks, large+small parcels, 3–6 continuous inlets | Yes |
| — | Train-style 2-way splitters + pause-belt switches; player operates the yard, does not carry | Yes |
| — | Always-running line: parcels, belts, auto equipment, on-time trucks; player reconfigures routes | Yes |
| — | Low-poly target; cubes now; replace art later | Yes |
| — | ≥5 levels, significant difficulty jumps; multi-round development | Yes |
| — | Upgrades must actually work in a round (scan, direction, auto-divider, warehouse, …) | Yes |
| 0 | Click-inspect first; buy gear; **manually install before the round** | Yes |
| 0 | Auto scanner shows destination; auto-sorter on a junction; temp warehouse holds a few then dump later | Yes |
| 1 | Inlet gate close for thinking time; auto-reopen if held too long | Yes |
| 3 | Some belts: click high speed, click again back to normal | Yes |
| 4 | Barcode + X-ray + auto-sorter; full info only after scan; info is a resource | Yes |
| 4 | Scan example: unlabeled/misleading crate → `[Blue] [Chilled] [Urgent]` | Yes |
| 8 | Trucks not static; capacity `0/20`; full → Depart → next truck; late `Blue truck ETA: 30s`; jam if pile grows | Yes |
| 10 | Six events with example timings: C belt 15s, red depart 10s, ×2 for 20s, labels 30s, oversize burst, Zone B closed | Yes |
| — | Score not just win/lose: S–D, seven axes, star card, grind motivation | Yes |
| — | Chat 简体中文; agent workflow + in-game UI English | Yes |
| — | New level = new JSON, modular load, not hardcoded | Yes |

---

## Final Goal

Ship a playable vertical product with **every item in the Canonical Design Brief**, including:

1. **At least 5 levels** with a real difficulty jump between each (not just faster spawn). Inlet count scales **3 → 6**. Each level is a **JSON config** loaded by a shared `LevelLoader` — a sixth level must not require new C#.
2. A **working meta upgrade loop**: start with **click-inspect only**; buy equipment between runs; **manually install into the yard before the round**. Scanner, X-ray, auto-sorter, warehouse, gates, speed kits must all change actual in-round behavior.
3. **Information as a resource**: destinations and tags hidden until inspect/scan. Pre-scan appearance is not the truth.
4. **Dynamic trucks**: capacity (kickoff `0 / 20`), `Depart` when full, next truck enters, late trucks with ETA (kickoff `Blue truck ETA: 30s`), schedule departures — not static colored holes.
5. **All six random event types** from the brief (belt fault, early truck, peak ×2, label failure, oversize burst, zone closed).
6. **Scoring**: stars + **S / A / B / C / D**, tracking correct rate, avg handling time, damage, jams, truck load, equipment use, energy. Grind for high scores, not only win/lose.
7. Placeholder **cubes now**; **low-poly** art swap later without a systems rewrite.
8. The **always-running line** fantasy: parcels, belts, auto-equipment, on-time trucks; player only reconfigures routes.

If a feature cannot be used in a real round, it is not done.

---

## Design Pillars

| Pillar | Meaning in play |
| --- | --- |
| The system keeps running | Parcels run. Belts run. Installed sorters work by themselves. Trucks leave on time. The player never “pauses the world” except via diegetic tools (gate, belt pause) that have costs. |
| Player is a dispatcher | Click-inspect, flip train-style 2-way splitters, close inlet gates, toggle belt speed, pause belts, place upgrades. Not a carrier. |
| Information is a resource | You do not know a parcel’s real destination (or chilled / urgent / size) until inspect or scan. Pre-scan looks can lie. |
| Upgrades are physical | Bought between runs, then **manually mounted on a specific belt / junction / bay before the round**. Unequipped = no effect. |
| Art is swappable | Cubes today, low-poly later. Same prefabs, different meshes. |
| Levels are modular | New yard = new JSON + existing modules. No hardcoded layouts. |

---

## Core Loop

```
INLETS (3–6, by difficulty)
    → optional inlet GATE (player can close; auto-reopens if held too long)
    → CONVEYORS (normal / high-speed toggle on some segments; pause switches)
    → JUNCTIONS (2-way splitters; later auto-sorters)
    → SCAN / INSPECT (reveal color, cold, urgent, size)
    → optional TEMP WAREHOUSE (hold a few parcels, release later)
    → TRUCK BAYS (color-coded, capacity, schedule, late ETA)
         → truck full or timer → DEPART → next truck enters (or wait with jam risk)
```

Player loop: **watch flow → gather info → reconfigure route → prevent jam / wrong truck / missed departure → score.**

---

## Feature Spec (target)

Numbers and UI strings below match the Canonical Design Brief. Tune later; do not drop a type.

### Spatial layout

- One shared 3D yard, left inbound / right outbound.
- **3–6 inlets** by difficulty, **continuous** spawn (not a fixed pile the player empties).
- Middle: maze of belts, including **train-turnout 2-way splitters** and **pause switches**.
- Right: **one truck bay per destination color** (more colors as levels grow).
- Camera should read the whole line; player operates the yard, does not walk parcels by hand.

### Packages

- Spawn from left inlets; travel left → right on belts; **always moving** while on an unpaused belt.
- Size classes: **small**, **large**, and **oversized** (events can flood oversized).
- Hidden until inspect or scan: **destination color**, **chilled / cold chain**, **urgent**, **size class**.
- Pre-reveal visual: **generic crate**. Outer look must not give away the real destination (kickoff: a “red box” that scans as Blue + chilled + urgent).
- After inspect/scan, English tags: `[Blue] [Chilled] [Urgent]` (add `[Large]` / `[Oversize]` when relevant).
- Wrong truck / crush / jam timeout = **damage** or **miss**, which feeds scoring.

### Conveyors & controls

- Belts **keep running** unless the player pauses that segment or an event stops a zone.
- **Some** segments: click → **high speed**; click again → **normal**. Not every belt has this (until a speed-kit upgrade unlocks a slot).
- **Some** switches: **pause** that conveyor (player tool; default world is not paused).
- Junctions: **2-choose-1**, train-switch feel. Player-operated first. An **auto-sorter** can be mounted on a junction to take over that choice.

### Inlet gates

- Each inlet can be **temporarily closed** so inbound parcels wait / do not enter, giving the player time to reconfigure.
- If closed **too long**, the gate **force-opens by itself**. The player cannot freeze inbound traffic forever.

### Scan equipment (three distinct machines + click-inspect)

- **Click-inspect:** default, always available early. Player must **click** a parcel to spend attention and reveal destination + tags. This is the career starting tool.
- **Barcode scanner:** belt-mounted. After a parcel **passes the scan point**, destination (and basic tags) **display automatically**.
- **X-ray machine:** belt-mounted, distinct from barcode. After passing, **full** hidden info shows (color + chilled + urgent + size).
- **Auto-sorter:** mounted on a **junction**. That split becomes automatic using scanned/revealed destination. Unrevealed parcels must **not** be perfectly auto-sorted for free (hold, default lane, or wrong-guess risk — pick one rule and keep it consistent).
- Unscanned / uninspected arrival at a truck is a **guess**. Information is a resource.

### Trucks (dynamic exits, not static holes)

- Color-coded bays. HUD example: `🚚 Red truck` + `0 / 20` (capacity **20** is the kickoff default; per-level JSON may override).
- Accepts matching color while docked.
- **Full** → `🚚 Depart` → truck leaves the bay → **next truck of that color enters** (after a delay if scheduled).
- Also leaves on a **timer / schedule**, not only when full.
- **Late truck:** empty bay + `Blue truck ETA: 30s`. Blue parcels **cannot stack forever**; overflow **jams the whole system**.
- Event can pull departure forward (`Red truck departs in 10s`).
- Wrong color into a truck = miss / damage.

### Temporary warehouse (upgrade)

- Place on a spur / warehouse slot before the round.
- **Holds several parcels** (small capacity, kickoff: “a few”).
- Player stores them to relieve a jammed or truck-missing line, then **releases them later** onto the belt when the matching truck is in or the path is clear.
- Must be usable in play (store + release), not decorative.

### Meta upgrades (between runs, install before round)

Shop / hub **between rounds**. Currency from score. Then **pre-round placement**: drag / snap owned items onto **legal slots** in the upcoming JSON level. Starting career: **no scanner** — click-inspect only.

| Equipment | Slot type | What it actually does in-round |
| --- | --- | --- |
| Barcode scanner | Scan slot on a belt | Auto-show destination after pass |
| X-ray | Scan slot (or X-ray slot) on a belt | Auto-show full tags after pass |
| Auto-sorter | Junction slot | That turnout routes automatically from revealed data |
| Inlet gate kit | Inlet slot | Enable close / auto-reopen if the layout slot allows it |
| Speed belt kit | Belt slot | Unlock high-speed toggle on that segment |
| Temporary warehouse | Warehouse slot | Hold a few parcels, release later |
| Inspect upgrade (optional QoL) | Player / global | Faster or longer-range click-inspect |

If it cannot be bought, mounted, and observed changing a round, it is not shipped. Selling or leaving a slot empty **reverts** that behavior.

### Events (mid-round, all six kickoff types)

Random from the level JSON table. Each round should feel different. Canonical examples (English banners):

- `⚠ Conveyor fault — Zone C belt stopped 15s`
- `⚠ Early truck — Red truck departs in 10s`
- `⚠ Peak rush — parcel volume ×2 for 20s`
- `⚠ Label failure — colors unreadable 30s` (auto-identify off; click-inspect remains the expensive fallback unless the event says otherwise)
- `⚠ Oversize burst — incoming oversized parcels`
- `⚠ Road closed — Zone B shut for maintenance`

Zones (A/B/C/…) are layout ids in JSON so events can target real modules.

### Scoring

Not success/fail only. Letter **S / A / B / C / D** plus a **star display**. The point is **replay / grind for high scores**.

Track **all** of: correct sort rate, average handling time, parcel damage rate, jam count, truck load rate, equipment use count, energy.

End card must be able to show the kickoff layout (stars, rank, correct %, avg delay, damage, jams). Other axes can sit on a details panel.

### Five levels (difficulty must change the problem, not only the clock)

### Five levels (difficulty must change the problem, not only the clock)

| Level | Inlets | Layout idea | What is new |
| --- | --- | --- | --- |
| **1 Tutorial yard** | 3 | Short belts, 1 splitter, 2 truck colors | Click-inspect, one junction, static-enough trucks so the verb is learned |
| **2 Split yard** | 3–4 | Extra loop, pause belt, inlet gate | Gates + speed toggle; first capacity trucks |
| **3 Scan yard** | 4 | Scan point required for full info; 3 colors | Hidden labels; late truck ETA |
| **4 Chaos yard** | 5 | Cross traffic, buffer slot, auto-sorter slot | Events start; warehouse + auto-sorter matter |
| **5 Hub peak** | 6 | Dense maze, overlapping events, tight ETAs | Full upgrade loadout; S-rank is the skill check |

Level data lives in **JSON** (inlet count, module graph, truck schedule, event table, allowed upgrade slots). The five launch levels are five config files, not five scenes. See **P2**.

---

## Current Status (as of 2026-08-17)

Update the checkboxes in this file after each development round.

### Project bootstrap

- [x] Unity 6 URP project exists
- [x] Sample scene with ground, camera, lighting
- [x] Placeholder materials (`ground`, `conveyor`, `package 1`, `package 2`)
- [x] Scene markers: `StartPoint`, `end1`, `end2`, sample package cubes
- [x] Empty `NewBehaviourScript.cs` replaced by real gameplay scripts
- [x] Folder / prefab / JSON config architecture in place
- [x] Input (click to select / interact) working
- [x] LevelLoader + module catalog (JSON → runtime yard)
- [x] Language split respected (chat 中文 / UI + code English)

### Gameplay systems

- [x] Conveyor movement (packages stick to belts, follow path)
- [ ] Package spawn from multiple inlets
- [x] 2-way junction / splitter
- [x] Belt pause switch
- [x] Belt high-speed toggle
- [x] Inlet gate (manual close + auto-reopen)
- [ ] Click-inspect (reveal destination / tags)
- [ ] Barcode scan station (reveal on pass)
- [ ] X-ray station (full tags on pass)
- [ ] Truck bays with color matching
- [ ] Truck capacity (`0 / 20`) + Depart + next truck
- [ ] Truck ETA / late arrival (jam if pile grows)
- [ ] Temporary warehouse (hold a few + release)
- [ ] Auto-sorter on a junction
- [ ] Wrong-sort / jam / damage tracking
- [ ] Event director (all six kickoff event types)
- [ ] Score: stars + letter rank + all seven axes
- [ ] Between-run shop
- [ ] Pre-round equipment placement on slots
- [ ] 5 authored JSON levels (3–6 inlets)
- [ ] Low-poly art pass (replace cubes)

**Overall: Phase 0 complete. Phase 1 complete. Phase 2 complete.**

---

## Development Phases (one round = one phase)

Do **not** try to finish the whole game in one pass. Each phase should end with something **playable in Unity**, then stop and review.

Art rule for every phase until Phase 10: **cubes, colored materials, world-space text/UI**. No blocking on models. Low-poly swap is last.

---

### Phase 0 — Foundation

**Goal:** A project we can extend without painting ourselves into a corner.

- [x] Confirm Unity 6 + URP + scene
- [x] Create folders: `Scripts/Core`, `Scripts/Belts`, `Scripts/Packages`, `Scripts/Trucks`, `Scripts/Equipment`, `Scripts/Meta`, `Scripts/UI`, `Scripts/Level`, `Configs/Levels`, `Prefabs/Modules`, `Art/Placeholders`
- [x] Delete or replace `NewBehaviourScript.cs`
- [x] Define data types (even if unused): `ParcelData`, `BeltSpeed`, `DestinationColor`, `LevelConfig` (JSON-serializable)
- [x] Sketch `LevelConfig` JSON schema + a module id list (do not hardcode a unique scene layout)
- [x] Camera: orbit or locked 3/4 view that can see the whole yard (tune later)

**Exit check:** Play mode opens the yard. No gameplay yet is OK. Schema is written down even if the loader is still stubbed.

---

### Phase 1 — Vertical slice: packages move and can be sorted

**Goal:** One inlet, a belt path, **one splitter**, **two colored exits**. Player flips the splitter. Packages that reach the matching exit “succeed.” The yard is **spawned from JSON**, even if that JSON is tiny.

Work order:

1. [x] Module prefabs: inlet, straight belt, junction, sink/truck placeholder.
2. [x] `LevelLoader` reads `StreamingAssets/Configs/Levels/level_01.json` (grid cells) and instantiates modules by id + pose + connections.
3. [x] Belt segments that move a follower along a waypoint path.
4. [x] Parcel prefab (cube + `Parcel` component: color assigned at spawn, visible tint **temporary** for this phase only — we will hide color in Phase 3).
5. [x] Spawner driven by JSON (interval, inlet id) — not a hardcoded `StartPoint` in C#.
6. [x] Junction: click to toggle path A / path B.
7. [x] Two sinks that count correct vs wrong.
8. [x] Destroy or pool parcels on exit.
9. [x] Debug HUD (English): `Spawned` / `Correct` / `Wrong`.

**Exit check:** You can stand in Play mode, flip one switch, and sort red vs blue by hand. Duplicating `level_01.json` as `level_01_copy.json` and pointing the loader at it produces the same yard with **no code change**. Met 2026-08-16.

**Camera:** Fixed Overcooked-style aerial / pseudo-isometric (perspective, pitch **75°**, frames the JSON yard). Player does not orbit or pan.

**Phase 1 wrap-up (2026-08-17):** Playable tutorial yard. Layout is a centered Y-split authored in `StreamingAssets/Configs/Levels/level_01.json` (`gridSize` 2, `mapCells` `[40, 24]`). One inlet on the spine, long thin belt segments, a clickable 2-way junction, then two diagonal-then-east lanes to stacked red/blue truck-bay sinks. `LevelLoader` + `ModuleCatalog` instantiate the yard; parcels follow kinematic waypoints. Junction output is **latched when a parcel enters the cell**, so toggling mid-crossing does not teleport it. Indicator is a yellow pointer along the live A/B exit. Debug HUD: `Spawned` / `Correct` / `Wrong`. Duplicate `level_01_copy.json` loads with no C# change.

**Out of scope:** scanners, trucks as vehicles, events, shop, levels 2–5.

---

### Phase 2 — Player tools: gate, pause, speed

**Goal:** The player can **buy time** and **change flow rate**, not only flip a splitter.

1. [x] Inlet **gate**: click to close; parcels stop spawning or queue behind the gate; **timer then force-open**.
2. [x] **Pause belt** on at least one segment (click).
3. [x] **High-speed toggle** on at least one other segment (click cycle: normal ↔ fast).
4. [x] Visual feedback: gate down, belt color/emission, speed chevrons (can be unlit cubes).
5. [x] Jam detection v0: too many parcels overlapping / stopped too long → counter ++.

**Exit check:** You can close the gate, pause a belt, and speed another, and the line behaves. Closing the gate forever is impossible.

**Phase 2 wrap-up (2026-08-17):** Tutorial JSON now flags `inlet_a` (`hasGate`), `belt_m4` (`canPause`), `belt_m7` (`canSpeed`). Closing the gate stops real spawns and stacks hopper placeholder cubes (cap 6); after `gateHoldMax` 8s it force-opens and resumes the interval (no dump). Pause sets `SpeedMultiplier` to 0; speed toggles 1 ↔ 2 with chevrons. HUD adds `Jams` and a `Gate Xs` countdown. Stall jam v0: a parcel stopped ≥ 3s counts once per stall episode.

**Out of scope:** meta shop. These tools can be “always available” on the tutorial layout until Phase 6 gates them behind upgrades.

---

### Phase 3 — Information as a resource + scan point

**Goal:** Stop giving free destination knowledge.

1. Spawn parcels as **generic crates** (same gray cube). Destination and tags exist in data only.
2. **Click-inspect**: raycast parcel → delay or hold → reveal color + tags (world UI or floating labels).
3. Uninspected / unscanned parcels arriving at a truck count as **guess** (wrong if mismatch).
4. **Scan station** prefab (barcode): trigger volume on a belt. After passing, reveal destination + tags automatically.
5. Tags in data from day one of this phase: `Chilled`, `Urgent`, `Large` / `Oversize` (even if only color is used for routing this phase).
6. Tutorial copy (English UI): `You cannot see the destination until you inspect or scan.`
7. Keep a second prefab stub for **X-ray** (can share the reveal API; X-ray reveals the full flag set). Distinct module type in JSON.

**Exit check:** Playing “blind” is hard; inspecting or routing through the scanner makes sorting possible. A crate that looked generic can scan as `[Blue] [Chilled] [Urgent]`. The scanner is a **layout choice**, not a HUD cheat.

**Out of scope:** buying extra scanners (that is Phase 6). One scanner baked into the test layout is enough.

---

### Phase 4 — Trucks are vehicles, not holes

**Goal:** Exits have **capacity, schedule, and absence**.

1. Truck bay prefab: color, `currentLoad / capacity`, docked / departed / incoming.
2. Docked truck accepts matching parcels until full **or** timer hits.
3. Full or timeout → play a simple depart (move the cube truck off-bay) → **next truck** after a delay.
4. **ETA** when the bay is empty: parcels that arrive with no truck must **queue or spill** (jam risk).
5. HUD per bay: `🚚 Red truck` + `12 / 20` and `Blue truck ETA: 30s` when relevant.
6. Wrong color into a truck = miss/damage.

**Exit check:** Filling a truck makes it leave. A late blue truck creates a real pile-up if you keep sending blue that way.

**Out of scope:** random “leaves 10s early” as a full event system (hook the API, fire it manually for now).

---

### Phase 5 — Events

**Goal:** Every round can go wrong in a different way.

1. `EventDirector` + event defs in JSON (duration, target zone id, parameters). Per-level event tables live in that level’s JSON.
2. Implement **all six** kickoff events (do not stub-and-forget):
   - Conveyor fault — target zone speed = 0 for **15s** (example: Zone C)
   - Early truck — pull departure forward (**10s** warning; example: Red)
   - Peak rush — spawn **×2** for **20s**
   - Label failure — auto-identify off for **30s**
   - Oversize burst — flood oversized parcels
   - Road closed — block a zone (example: Zone B)
3. Warning banner UI (English), matching the Canonical Design Brief strings.
4. Cooldown / readable stacking so the player can parse the banner.

**Exit check:** Two playthroughs of the same layout feel different because events hit different subsystems.

---

### Phase 6 — Upgrades that actually work (meta + install)

**Goal:** Between runs you **buy**, then **before the round you mount** equipment on slots. In-round you see the difference.

1. Hub scene or overlay: currency from last score, shop list.
2. Inventory of owned equipment (counts).
3. Level has **slots**: `ScanSlot`, `XraySlot` (or shared scan slot with type), `SorterSlot`, `WarehouseSlot`, `SpeedKitSlot`, `GateSlot`.
4. Pre-round placement mode: snap owned item onto a compatible slot (or skip).
5. Runtime:
   - Barcode scanner in slot → auto destination after pass.
   - X-ray in slot → full tags after pass.
   - Auto-sorter in slot → that junction routes by **revealed** destination (unrevealed = do not auto-route perfectly).
   - Warehouse in slot → store up to a few parcels, release onto the belt later.
6. New players: **no scanner**. They must click-inspect. Buying a scanner is the first meaningful purchase.

**Exit check:** A round with nothing installed is click-inspect hell. The same round with a scanner + auto-sorter on one junction is a different game. Selling/unequipping reverts behavior.

**Do not** ship shop UI whose items do not change the level.

---

### Phase 7 — Scoring + fail/success

**Goal:** Players want to **grind** for rank, not only “did the shift end.”

1. Track **all seven** axes: correct %, avg handling time, damage rate, jam count, truck load %, equipment use count, energy.
2. Weights → numeric score → letter **S A B C D** + star display.
3. Soft fail vs hard fail: too many wrongs, or the clock, end the shift; **jams never do**. Jam
   count is a scored axis (it drags the rank down), not a lose condition — see the 2026-08-30 row
   in the Status Log.
4. End-of-round card matching the brief (`⭐⭐⭐⭐⭐`, Rank S, Correct 99.8%, Avg delay 1.3s, Damage 0, Jams 0).
5. Currency payout from rank for Phase 6 shop.

**Exit check:** Two different play styles produce two different letters. S is possible but not free.

---

### Phase 8 — Five levels (five JSON files)

**Goal:** Five authored yards; difficulty is **structural**. Authoring = JSON, not new scenes or new scripts.

1. Finalize `LevelConfig` JSON schema: inlet count (3→6), module list, connections, truck colors/schedules, spawn curve, event table, slot layout, starting equipment rules.
2. Build layouts L1–L5 as `level_01.json` … `level_05.json`. Same module prefabs; **zero** duplicated gameplay scripts.
3. Level select reads a catalog JSON (list of level ids / display names) and feeds the loader.
4. Difficulty checks:
   - L1: learn inspect + one splitter + two trucks.
   - L2: gate + speed + capacity.
   - L3: must scan; 3 colors; ETA gaps.
   - L4: events + warehouse useful.
   - L5: 6 inlets, overlapping events, S-rank requires a planned loadout.
5. Playtest each level for “new problem,” not only “faster.”
6. Smoke test: add a throwaway `level_sandbox.json` without touching C#. If that fails, the loader is not done.

**Exit check:** A player who mastered L1 still has to think on L5. A sixth level can be added by dropping in a JSON file and catalog entry only.

---

### Phase 9 — Feel, juice, placeholder presentation

**Goal:** Readable low-poly **stand-in** so later art has a target.

1. Distinct silhouettes for: small parcel, large parcel, truck, scanner, sorter, warehouse, gate, splitter handle.
2. Color language: destination colors only **after** reveal; belts/floor stay neutral.
3. Simple audio: belt hum, stamp/scan beep, truck horn, alarm for events (can be temp).
4. Camera framing per level.
5. Replace debug text with minimal world UI.

**Exit check:** A screenshot reads as “sorting facility,” still cubes.

---

### Phase 10 — Art swap (low-poly)

**Goal:** Drop in models without touching gameplay code.

1. Keep the same prefab roots and colliders.
2. Swap meshes/materials per prefab (parcels, belts, trucks, machines, environment).
3. Animation: truck in/out, gate, splitter lever, scan light.
4. Lighting / URP volume polish.

**Exit check:** Play L1–L5 with art. No script rewrite. Hitboxes still match visuals.

**Do this last.** Gameplay and five levels come first.

---

## Suggested Round Order vs Features

| Your feature | First playable in | Must be “real” by |
| --- | --- | --- |
| Click-inspect (career start) | Phase 3 | Phase 3 |
| Inlet gate + auto-reopen | Phase 2 | Phase 2 |
| Belt pause switch | Phase 2 | Phase 2 |
| Belt high-speed toggle (click again to revert) | Phase 2 | Phase 2 |
| Barcode scanner | Phase 3 | Phase 6 (placeable) |
| X-ray | Phase 3 (stub OK) | Phase 6 (placeable, distinct) |
| Auto-sorter on a junction | Phase 6 | Phase 6 |
| Temporary warehouse (hold a few, release later) | Phase 6 | Phase 6 |
| Truck capacity `0/20` + Depart + next truck | Phase 4 | Phase 4 |
| Late truck ETA + jam if pile grows | Phase 4 | Phase 4 |
| All six events | Phase 5 | Phase 8 (per-level tables) |
| Stars + S/A/B/C/D + seven score axes | Phase 7 | Phase 7 |
| Hub shop + pre-round manual install | Phase 6 | Phase 6 |
| JSON LevelLoader + modules | Phase 1 | Phase 1 (required; do not skip) |
| 5 levels, 3–6 inlets, difficulty is structural | Phase 8 | Phase 8 |
| Low-poly art (replace cubes) | Phase 10 | Phase 10 |

---

## Architecture Notes (so later rounds do not rewrite Phase 1)

- **JSON** for levels (required) and preferably for events / score weights / equipment defs. ScriptableObjects are OK for Unity-side module prefab references (the **catalog**), not for per-level layouts.
- **LevelLoader**: `StreamingAssets/Configs/Levels/*.json` → instantiate modules from a ScriptableObject catalog (`type` → prefab). Layout uses **grid cells** (`cell: [x, z]`, default `gridSize` 4). Connections must be 4-adjacent. Waypoints are stitched at runtime (inlet/belt/junction/bay).
- **Suggested JSON sketch** (evolve the schema; do not hardcode this layout in C#):

```json
{
  "id": "level_01",
  "displayName": "Tutorial Yard",
  "gridSize": 4.0,
  "modules": [
    { "id": "inlet_a", "type": "inlet", "cell": [0, 0], "rotationY": 0 },
    { "id": "belt_01", "type": "belt_straight", "cell": [1, 0], "rotationY": 0 },
    { "id": "junc_01", "type": "junction_2way", "cell": [2, 0], "rotationY": 0 },
    { "id": "belt_a", "type": "belt_straight", "cell": [3, 0], "rotationY": 0 },
    { "id": "bay_red", "type": "truck_bay", "cell": [4, 0], "rotationY": 0, "color": "Red" },
    { "id": "belt_b", "type": "belt_straight", "cell": [2, -1], "rotationY": 0 },
    { "id": "bay_blue", "type": "truck_bay", "cell": [3, -1], "rotationY": 0, "color": "Blue" }
  ],
  "connections": [
    { "from": "inlet_a", "to": "belt_01" },
    { "from": "belt_01", "to": "junc_01" },
    { "from": "junc_01", "output": "A", "to": "belt_a" },
    { "from": "belt_a", "to": "bay_red" },
    { "from": "junc_01", "output": "B", "to": "belt_b" },
    { "from": "belt_b", "to": "bay_blue" }
  ],
  "spawn": { "interval": 2.0, "inlet": "inlet_a" },
  "slots": [],
  "events": []
}
```

- **Slots** are empty transforms with a type enum, spawned from JSON. Equipment instances enable/disable child behaviours.
- **Parcels** store `isRevealed`, `destination`, `flags`. Visuals subscribe to `isRevealed` — never bake color into the mesh until revealed.
- **Belts** expose `speedMultiplier` and `blocked` so events and player toggles share one path.
- **Trucks** expose `Accept(parcel)`, `Depart()`, `Eta`. Sinks in Phase 1 should be replaced by this API in Phase 4, not left as a second scoring path.
- **No singleton soup**: a `YardDirector` per scene is enough (spawn, score, events).
- **Pooling** for parcels once spawn rate goes up (Phase 4+).
- **UI strings**: English only. Centralize in one place so we never scatter Chinese into HUD text.

---

## How to Use This File Each Round

This file **replaces** the original design chat. Future agents: read this README first; do not ask the user to resend features.

1. Treat **Canonical Design Brief** as intent + examples. Treat **Feature Spec** and **Phases** as how we implement it.
2. Pick **one phase**.
3. Only uncheck items inside that phase.
4. Hit the **exit check** in Play mode before starting the next phase.
5. Update **Current Status** checkboxes.
6. If a phase is too big for one session, split it at a numbered work-order item, not by “a bit of everything.”
7. Keep **P1** (language split) and **P2** (JSON levels) in every round. Do not “temporarily” hardcode a layout.
8. If a kickoff example (capacity 20, ETA 30s, six events, seven score axes, three machines, inspect-first) is missing from the build, it is not “paraphrased away” — put it back.

---

## Status Log

| Date | Phase | Note |
| --- | --- | --- |
| 2026-08-16 | 0 | Project created. Cubes + placeholder mats. `StartPoint`, `end1`, `end2` in SampleScene. No gameplay scripts yet. README added. |
| 2026-08-16 | 0 | Canonical Design Brief added: full kickoff coverage (inspect-first, three machines, gates, speed toggle, dynamic trucks, six events with timings, seven score axes, 3–6 inlets, always-running fantasy, cubes→low-poly, JSON levels, language split). Future agents should not need the original chat. |
| 2026-08-16 | 1 | Vertical slice: grid JSON `LevelLoader`, waypoint parcels, clickable 2-way splitter, red/blue sinks, English HUD. Camera is fixed Overcooked aerial. `level_01_copy.json` loads with no C# change. |
| 2026-08-17 | 1 | Phase 1 closed. Centered Y-split layout, expandable `mapCells`, thin belt segments, pitch 75°, junction path latch, yellow A/B indicator. Ready for Phase 2 (gate / pause / speed). |
| 2026-08-17 | 2 | Gate / pause / speed on tutorial JSON. Hopper backlog cubes (no dump on open), force-open at 8s, stall jam HUD. |
| 2026-08-30 | 2 | Round controls + rules pass. (a) New bottom-left `RunControlPanel`: `PAUSE` / `RESUME` and `EXIT LEVEL`, shown during Running and Result. Pause freezes traffic, spawning, the mission clock **and** `Time.timeScale`, so gate holds and AutoArm cooldowns stop too; yard clicks are refused while paused. Exit abandons the round with no payout and no record, and returns to prep with every placed device untouched. (b) **The jam cap is gone**: `maxJams` no longer exists in `MissionObjective`, the parser, or `level_01.json`, and `MissionFailReason.TooManyJams` is retired. Jams are still counted and displayed. (c) Devices already on the yard can be **dragged from slot to slot during prep**. Each device gained an idempotent `Uninstall()`, the same instance is re-seated (no stock changes hands), and a refused move rolls back to the original slot. A press that does not travel 14px is treated as a click, not a move. |

Add a row after every development round.
