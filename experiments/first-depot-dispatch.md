# First Supply Depot dispatch experiment

## Purpose

The production rule for this experiment is now explicit: **reserve the first Supply Depot SCV after 9.0 seconds of measured wall-clock time from the first match observation**. The first bot update at or after that elapsed-time boundary triggers the reservation. SC2 game-loop counts are not used for this decision because their relationship to wall-clock time depends on game speed. The worker is reserved at that trigger even if it is carrying minerals; it completes its return/unload before receiving the construction move. Mineral-bank forecasts and travel estimates are retained for diagnostics, not as gates on the pull.

The previous predictive rule pulled the SCV at game loop 124 (about 5.54 seconds) with 90 minerals in the latest committed reference trace, then did not issue the Build command until loop 279 with 100 minerals. The observed move-to-build interval was 155 loops (about 6.92 seconds). That is the concrete early-pull baseline this change replaces.

The purpose of subsequent experiments is to measure the result of the fixed nine-second wall-clock trigger and refine later opening steps without silently changing this first pull time.

This is a controlled measurement exercise, not a new hard-coded build order. The live observation, SC2 game data, placement result, resource bank, prerequisites and action result remain authoritative.

## Experimental rule

Change one independent variable per comparison. Keep map, opponent, race, game speed, starting conditions, bot commit, dependency commits, and every other policy setting fixed. Record any unavoidable match-to-match differences. Use paired runs and compare distributions, not a single lucky result.

Do not compare a run that changes worker selection, pull threshold, travel estimator and resource forecast simultaneously. That would not identify the cause of an improvement.

## Fixed checkpoint: pineapple at 20 seconds

At 20.0 seconds of match game time, emit or capture a pineapple checkpoint containing:

- game loop and the conversion used to derive elapsed seconds;
- mineral and vespene bank, used/cap supply, and current income/worker counts;
- every live SCV's tag, position, cargo state, observed order/target, assignment, reservation and current FSM state;
- candidate builder identity and the reason it was selected;
- candidate Depot placement, placement-query status and pathing/travel estimate;
- predicted time to reach the site, predicted time until the Depot is affordable, and predicted arrival-to-affordability gap;
- actual builder travel elapsed so far, if dispatched;
- first Depot move, affordability, build-command and completion game loops when those events have occurred;
- active construction/resource reservations and any action rejection.

The checkpoint is a snapshot, not a command. It must not change the bot's decisions. If the match has not reached a milestone by 20 seconds, record it as pending rather than filling in an estimate as an observed fact.

Use the actual game-loop-to-seconds conversion for that match. Never assume a fixed loop rate or label a loop count as seconds without recording the conversion.

## Baseline

The superseded reference trace used an 80-mineral pull threshold and selected the SCV at loop 124. The production target is now time-based: first eligible observation at or after loop 202 (9.0 game seconds). Mineral availability does not delay or advance worker selection. The actual Build action still requires the live mineral bank and a valid placement.

The current opening diagnostics are expected to include builder selection, first move, first affordability, build command and observed completion loops. Worker and economy traces provide per-observation positions, cargo, orders, target identity, resources and structure milestones. Confirm those files exist before beginning the comparison.

## Independent experiment dimensions

### A. Worker selection — hold dispatch and estimation policy fixed

Compare one policy at a time:

1. Current production policy (baseline).
2. Prefer an empty-handed eligible SCV.
3. Prefer an SCV already returning to the Command Center, including a cargo-bearing miner, but dispatch only after its cargo is unloaded.
4. Prefer the eligible SCV with the lowest observed pathing cost to the accepted Depot site.
5. Prefer the eligible SCV with the lowest predicted total opportunity cost: time to reach the site plus the mining cycle that would be interrupted.

Do not assume the nearest worker is best. A worker carrying minerals may be close to the site but still needs to unload; an empty-handed worker may be farther away but arrive sooner. Record the candidate set and rejection reasons so selection is auditable.

### B. Travel-time estimator — hold worker policy and pull threshold fixed

Compare:

1. Straight-line distance divided by a measured/declared worker movement-speed estimate (proxy only).
2. SC2 pathing distance divided by the same speed estimate.
3. An empirical route-time estimate derived from prior observed SCV movement on the same map/route class.

Record estimator inputs, confidence, prediction timestamp, predicted arrival loop, and actual arrival loop. Keep query latency and stale/missing pathing results explicit. A pathing distance is not itself a travel time.

### C. Resource-arrival estimator — hold worker policy and travel estimator fixed

Compare:

1. Current observed-bank threshold policy (baseline).
2. Forecast affordability from a recent observed mineral-income rate.
3. Forecast affordability from per-worker route/service-cycle estimates, including cargo already in transit.

The forecast schedules a dispatch; it never authorizes construction. The actual observed bank and authoritative game-data cost remain the construction gate. Track forecast error as predicted affordable loop versus actual affordable loop.

### D. Pull threshold — hold all other policies fixed

Retain the existing threshold as the control, then compare the documented 70, 90 and 100 mineral candidates. Treat these as experimental inputs, not claims about optimal timing.

## Measurements

Report at minimum:

- builder selected, selection loop, dispatch loop, site-arrival loop;
- first affordable loop, build-command loop and observed completion loop;
- travel time and estimate error;
- resource forecast error and time waiting at site for affordability;
- mineral income and bank at pineapple (20 seconds);
- SCV time in mining, carrying, returning, unloading, travelling to construction, building, idle and reserved states;
- minerals delivered or lost opportunity, with inferred per-worker income clearly labeled as derived;
- supply-block duration, first Depot completion, first Barracks start/completion, SCV production continuity and downstream Marine production uptime;
- rejected/stale commands, reassignment churn and reservation conflicts.

A faster Depot is not a win if it causes a longer resource drought, delays SCV production, increases idle builder time, or starves Barracks/Marine production. Evaluate the complete opening.

## Run sequence and evidence

1. Capture one unmodified baseline run with diagnostics enabled.
2. Preserve its raw diagnostic bundle and record exact bot, FSM_API and other dependency commit SHAs.
3. Capture the pineapple checkpoint at 20 seconds.
4. Change exactly one experiment dimension and repeat under the same conditions.
5. Repeat enough paired runs to expose variance; do not declare a winner from one match.
6. Compare the event loops and worker-cycle classifications using the same analysis version.
7. Only after the data identifies a useful policy should it become the production default, with regression tests for the selected behavior.

## Live-run handoff

The operator should provide the complete diagnostic bundle for each requested run, not screenshots alone: console output, opening trace, worker CSV, economy CSV/timeline and any run metadata. Preserve the original files unchanged. Redact personal filesystem paths if needed, but do not remove game-loop values, unit tags, event names or policy settings.

No live result is inferred from unit tests, a successful launch, or a generated checkpoint. Match evidence must be labeled separately from source implementation and CI evidence.
