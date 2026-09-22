# RT pack — balance notes

How the numbers in this pack's jobs and reward collections were derived.
All prices come from THIS modpack's def files (`Description.Cost`), never from
vanilla data. Anchors differ by job class:

## CollectItems (Deliver)

Reward ≈ worst-case batch value: `MaxTargetCount × top-of-band item price`.
Deliver consumes the items, so the payout must cover what the player gives up.
The sell alternative (shop at the career "Shop Selling Prices" slider, 5–18% in
this pack) is far below every payout — by design.

| Job | Price band | Worst batch | Reward |
|---|---|---|---|
| collectItemsCheap | 2–11.25k × 6–12 | ~135k | 120k |
| collectItemsMid | 27–40k × 4–8 | 320k | 320k |
| collectItemsExpensive | 100–125k × 3–5 | ~625k | 650k |

## Reward collections (rolls)

Expected value per roll = Σ(count × weight × cost) / Σ(weight), kept at
~18k / 88k / 176k for Tier1/2/3 (ComStar ~148k, Exotic ~101k, Quicsell ~18.3k and QuicsellStore ~55.5k —
the joke-brand collections: ammo locked to Tier1 parity, the gear boutique sits
between tiers deliberately as the collect job's payoff; MechPart entries are
valued at chassis cost / parts-to-assemble — one part, not the whole mech). When adding entries, keep the EV within
±0.5% — recalculate, don't eyeball.

## CollectMech / CollectVehicle (Deliver since v0.7 — the unit/parts are handed over)

v0.7 turned these jobs consuming, so the anchor moved from "bounty for owning"
to **trade**: the payout must clear the player's real alternative — selling the
unit at the career "Shop Selling Prices" slider (~13% in this pack) — with a
premium for the buyer's specific request.

- **Deliver anchor** = shop-sell value × ~1.5 premium.
- The mech ladder (150/250/400/600k) already clears the sell line on every tier
  (2–6× vs sell) — numbers unchanged; the historic 8–10%-of-floor bounty
  happened to land above the new anchor.
- Vehicles sat BELOW the sell line (0.74–0.91×) — strictly dominated by selling;
  lifted to the anchor (22.09): **160k / 230k / 300k / 350k** (L/M/H/A).

| Tier | Pool floor | Sell ×0.13 | Reward |
|---|---|---|---|
| Mech light / medium / heavy / assault | 92k / 556k / 596k / 794k | 12k / 72k / 77k / 103k | 150k / 250k / 400k / 600k |
| Vehicle L / M / H / A | 568k / 1,040k / 1,254k / 1,350k | 74k / 135k / 163k / 175k | **160k / 230k / 300k / 350k** |

Parts jobs inherit ×1.33 of the whole-unit job of the same tier (vehicles:
210/300/400/460k; mech parts unchanged at 200/300/450/800k). The min-vs-median
family spread inside a tier (light pools hold both 92k and 537k families) is an
accepted constant-reward compromise: cheap-family resolves pay a jackpot
relative to their sell value, expensive ones stay comfortably above it.

## Destroy jobs

No market anchor — effort-based tiers (200k / 300k / 500k, legendary 600k).
Balance target: c-bills per kill vs the NET cash of contracts (the nominal
payout is cut by the cash/salvage slider, and salvage liquidates at the sell
or scrap modifier — never compare against the nominal value):

- destroyChassisLight 250k / 3–4 → 62–83k per kill
- destroyChassisMedium 350k / 2–3 → 117–175k per kill
- destroyChassisHeavy 450k / 2–3 → 150–225k per kill (initial 650k / 1–2 was
  325–650k per kill — trimmed)

## Career economy scaling

At payout time c-bills are multiplied by `Finances.ContractPricePerDifficulty /
200000` — the career "Contract Payment" slider (Cheapskate 50% … Generous 150%).
200k (Normal) is the baseline all numbers above are balanced against. Target
counts and item rolls are never scaled.
