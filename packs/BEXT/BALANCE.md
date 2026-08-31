# BEXT pack — balance notes

All prices come from the VANILLA base game defs (`StreamingAssets/data`:
`weapon`, `ammunitionBox`, `heatsinks`, `jumpjets`, `upgrades`). The short
RT-style ids (`Weapon_Laser_Medium`) are RT-made items and do not exist here;
vanilla ids carry the `_0-STOCK` / manufacturer suffix. BEXT references plain
vanilla ids heavily in its collections (MediumLaser_0-STOCK x250, PPC x234),
so every pool id below circulates in the BEXT economy. `_NU_` families
(Gauss, Pulse) are CAC automerge derivatives without static costs — skipped.

## CollectItems (Deliver)

Reward = worst-case batch value (`MaxTargetCount` x top-of-band price), BEXT
economy runs noticeably richer than RT so absolute numbers scale up:

| Job | Band | Worst batch | Reward |
|---|---|---|---|
| collectItemsCheap | 10-20k x 6-12 | 240k | 240k |
| collectItemsMid | 50-100k x 3-5 | 500k | 500k |
| collectItemsExpensive | 140-220k x 2-3 | 660k | 650k |

## Reward collections (rolls)

EV per roll: Tier1 44.9k / Tier2 64.8k / Tier3 163.3k (vanilla costs,
weight-tuned; entries restricted to ids confirmed circulating in BEXT).

## Destroy jobs

Per-kill anchoring against BEXT contract payouts: light 250k/4-8 = 31-62k,
medium 450k/3-5 = 90-150k, heavy 700k/2-4 = 175-350k (includes the
assault-mech line). Tag vocabulary: mech weights L355/M428/H481/A453,
vehicles 859 defs (VTOL 68), turrets 49.

## Chassis jobs

Families are chassis UIName values (SMA default without CC components):
multi-word keys carry spaces (`Phoenix Hawk`, `Mad Cat`). Pools curated by
variant count per family (Locust 11, Phoenix Hawk 13, Warhammer 18,
BattleMaster 18...). Rewards: destroy 300/450/600/700k, collect 250/400/500/600k,
parts 300/400/500/600k (L/M/H/A) — ladder continuation, EV-checked tiers ride
on top where applicable.

## Difficulty scale

BEXT star systems run a 10-tier scale (data: DefaultDifficulty -2..10, bulk at
1-9; the RT pack uses 1-20). All job eligibility ranges are halved accordingly:
cheap/light 1-3, mid/medium 3-6, expensive/heavy 5-8(10), assault 8-10,
parts tiers stepped 2-5/4-7/6-9/8-10. Porting RT ranges verbatim would make
assault jobs unreachable (15-20 never matches) and heavy ones half-gated.

## Career economy scaling

Same as RT: c-bills x `ContractPricePerDifficulty / baseline` at payout.

## Map coverage

2894 star systems across the merged map data; `planet_pop_large` (default
PlanetTag) covers 540 systems (~19%).
