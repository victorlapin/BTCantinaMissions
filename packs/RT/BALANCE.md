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
~18k / 88k / 176k for Tier1/2/3 (ComStar ~148k, Exotic ~101k, Quicsell ~18.3k —
the joke-brand collection, EV-locked to Tier1 parity so swapping it into a job
changes the flavor, not the value). When adding entries, keep the EV within
±0.5% — recalculate, don't eyeball.

## CollectMech / CollectVehicle (Acquire — the unit stays with the player)

Bounty, not value: ~8–10% of the pool floor (cheapest variant of the cheapest
family — worst-case orientation; richer families are a bonus).

| Tier | Pool floor | Reward |
|---|---|---|
| Mech light / medium / heavy | ~1.9M | 150k / 250k / 400k |
| Vehicle L / M / H | 568k / 1,040k / 1,254k | 60k / 100k / 125k |

Parts jobs inherit ×1.33 of the whole-unit job of the same tier (the original
tuning ratio: 200/300/450k vs 150/250/400k; vehicles mirror it).

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
