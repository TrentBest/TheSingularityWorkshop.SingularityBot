# Match postmortem — 2026-11-11 (Acolyte LE)

## Evidence

The local match was reported as a loss by the operator. The diagnostic bundle for the run is attached to [SC2 Diagnostics workflow run 42979956274](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/42979956274).

The captured timeline reaches game loop 5603 (about 250 seconds at 22.4 game loops per second). It records:

- 24 SCVs, 2 completed Barracks, 2 Supply Depots and a peak of 7 Marines.
- First through seventh Marines observed at approximately 104, 122, 140, 155, 158, 173 and 175 seconds.
- No second Command Center, Orbital, Refinery or Engineering Bay was observed in this capture.
- At the final recorded frame: 2,110 minerals, 0 vespene, 30/31 supply, 24 SCVs, 3 Marines and 2 Barracks.
- Enemy contacts appeared around 217 seconds. The enemy base and its Factory, Orbital, Barracks, Refinery, Reaper and Marines became visible around 235–240 seconds.
- Our Marine count fell from 7 to 3 between approximately 243 and 246 seconds.

The bundle does not contain an authoritative game-result record, so the win/loss is operator-reported; the unit and economy observations above are directly recorded telemetry.

## What failed

### 1. Reconnaissance became an unsupported incursion

The rally controller used an 8-unit stand-off from the enemy start and sent the assembled four-Marine group toward that point. The recorded movement orders place Marines around the enemy start area before the enemy's defensive composition was fully revealed. The group then encountered a Reaper and enemy Marines and lost four units in a short window.

This is not the desired scouting behavior. We need to observe the ramp from outside the enemy defensive area, preserve the scout's vision, and require an explicit force/engagement decision before moving beyond that observation position. The current stand-off is a conservative geometric heuristic, not a map-derived ramp detector; a live match must still verify the exact bottom-of-ramp location.

### 2. Reinforcements did not stream into the dispatched formation

The rally controller selected only the first four live Marine tags. Later Marines were not incorporated into the dispatched formation. This left the forward element without a reliable reinforcement stream.

The controller now keeps the initial four-Marine rendezvous gate, then includes later Marines in the same formation after dispatch. Formation slots are stable as the squad grows so new arrivals do not force existing Marines to reshuffle.

### 3. Expansion placement stalled the production plan

The second-Barracks controller correctly held Barracks three behind its Command Center gate, but the expansion controller never observed a valid Command Center placement. The planner stopped after generating nine candidate positions around the single nearest mineral cluster. The trace repeatedly records nine rejected placement results, while the expansion never starts. That leaves the bot at two Barracks and allows the mineral bank to accumulate without converting it into additional production or a second economy.

The planner now submits candidates from up to four ranked friendly mineral clusters in one placement query. This gives SC2 a chance to accept a different expansion when the nearest mineral line has no legal town-hall footprint. The controller still requires SC2 to confirm placement; no coordinates are treated as legal merely because the planner generated them.

### 4. Worker pathing evidence was unavailable

The initial worker pathing query returned 104 results with zero valid distances. The allocator therefore had to rely on its documented geometric fallback. Workers still reached a 24-SCV economy, so this was not the immediate combat failure, but pathing-aware worker allocation remains unverified on this map.

## Changes made from this postmortem

- Increase the Marine observation waypoint stand-off from 8 to 30 map units. The 30-unit stand-off keeps the waypoint beyond the current 11-unit weapon-plus-pursuit envelope for contacts near the enemy start.
- Keep the first-four assembly gate, then stream later Marines into the observation formation.
- Preserve existing formation slots as reinforcements arrive.
- Query expansion placements across multiple friendly mineral clusters instead of retrying only one blocked cluster.
- Add regression tests for safe stand-off, reinforcement arrival, stable formation slots and multi-cluster expansion fallback.

## Next match acceptance criteria

1. The first scout reaches the bottom-ramp observation area and remains there without climbing or entering the enemy's top-ramp defensive range.
2. The first four Marines assemble only after the fourth enters production; the group holds at the observation point rather than auto-invading the enemy base.
3. Later Marines visibly join the same formation.
4. Enemy contact triggers a force-balance decision; unsupported Marines withdraw or wait for reinforcement instead of feeding into the enemy army.
5. Expansion placement selects a valid friendly mineral cluster, or reports a specific fail-closed reason without permanently blocking all production.
6. Four-Barracks progress, SCV uptime, supply blocks, Marine production and the mineral/gas bank are measured through the next match.

CI success proves the code and tests, not these match outcomes. The next local SC2 run is required to verify the changes.
