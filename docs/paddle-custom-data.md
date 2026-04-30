# Paddle `custom_data` reference

A focused reference on the `custom_data` shape we expect on Paddle **products** so the sync can resolve discriminator columns. For end-to-end setup (api keys, webhooks, running the sync) see [paddle-setup.md](paddle-setup.md).

---

## What `custom_data` is for

Paddle gives every product, price, customer, transaction, and subscription a free-form `custom_data` JSON object. We use it on **products** as a small, machine-readable contract that lets the sync answer two questions without parsing the rest of the payload:

1. **What kind of product is this?** — service or addon
2. **Which app concept does it map to?** — which `ServiceType` enum value, or which addon code

The sync (`PaddleProductSyncService`) reads four well-known keys from `custom_data` and writes them to columns on `paddle_products`:

| `custom_data` key | Maps to column | Required for | Notes |
|---|---|---|---|
| `kind` | `paddle_products.Kind` | every product | `"service"` or `"addon"`. Anything else is mirrored but ignored by the catalog endpoint. |
| `service_type` | `paddle_products.ServiceType` | services | Must match a `ServiceType` enum value (`qpAdm` or `g25`). |
| `parent_service_type` | `paddle_products.ParentServiceType` | addons | Same enum domain — the service the addon attaches to. |
| `addon_code` | `paddle_products.AddonCode` | addons | Stable code for fulfillment-flag detection. The known codes today: `EXPEDITED`, `Y_HAPLOGROUP`, `MERGE_RAW`, `G25_ADMIXTURE`, `G25_PCA`. |

Any keys you put in `custom_data` outside that set are still mirrored to `paddle_products.CustomData` (jsonb). No business logic reads them, but they're available for future use without a schema change.

---

## Service products

A **service** is a top-level thing the user buys (qpAdm analysis, G25 analysis). One Paddle product per service.

### qpAdm

```json
{
  "kind": "service",
  "service_type": "qpAdm"
}
```

**Resolves to:**

```sql
paddle_products:
  Kind         = 'service'
  ServiceType  = 'qpAdm'
```

Catalog endpoint exposes it under `serviceType: "qpAdm"`.

### G25

```json
{
  "kind": "service",
  "service_type": "g25"
}
```

**Resolves to:**

```sql
paddle_products:
  Kind         = 'service'
  ServiceType  = 'g25'
```

---

## Addon products

An **addon** is an extra a customer can attach to a service order (Expedited processing, Y haplogroup, Raw merge, …). One Paddle product per addon.

### Expedited (attaches to qpAdm)

```json
{
  "kind": "addon",
  "parent_service_type": "qpAdm",
  "addon_code": "EXPEDITED"
}
```

**Resolves to:**

```sql
paddle_products:
  Kind                = 'addon'
  ParentServiceType   = 'qpAdm'
  AddonCode           = 'EXPEDITED'
```

When this addon is on an order, `QpadmOrder.ExpeditedProcessing` is set to `true` (the fulfillment-flag detection in `OrderPricingService` matches on `AddonCode`).

### Y haplogroup

```json
{
  "kind": "addon",
  "parent_service_type": "qpAdm",
  "addon_code": "Y_HAPLOGROUP"
}
```

Sets `QpadmOrder.IncludesYHaplogroup = true` when ordered.

> The frontend currently hides this code from the storefront — it's listed in `HIDDEN_STOREFRONT_ADDON_CODES` in `odin-react/src/api/catalog.ts`. The Paddle product still needs to exist so admin/back-office flows can reference it.

### Raw merge

```json
{
  "kind": "addon",
  "parent_service_type": "qpAdm",
  "addon_code": "MERGE_RAW"
}
```

Sets `QpadmOrder.IncludesRawMerge = true` when ordered.

### G25 admixture (attaches to G25)

```json
{
  "kind": "addon",
  "parent_service_type": "g25",
  "addon_code": "G25_ADMIXTURE"
}
```

### G25 PCA (attaches to G25)

```json
{
  "kind": "addon",
  "parent_service_type": "g25",
  "addon_code": "G25_PCA"
}
```

---

## End-to-end example: qpAdm + addons

Five Paddle products with these `custom_data` blocks, plus one active **price** on each:

| Paddle Product | Price (one-time) | `custom_data` |
|---|---|---|
| qpAdm Ancestry Analysis | $49.99 | `{ "kind": "service", "service_type": "qpAdm" }` |
| Expedited Processing | $20.00 | `{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "EXPEDITED" }` |
| Y Haplogroup | $20.00 | `{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "Y_HAPLOGROUP" }` |
| Merge Raw Data | $40.00 | `{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "MERGE_RAW" }` |

After running `POST /api/admin/paddle/sync/products`, `GET /api/catalog/products` returns:

```json
[
  {
    "serviceType": "qpAdm",
    "paddleProductId": "pro_01...",
    "paddlePriceId": "pri_01...",
    "displayName": "qpAdm Ancestry Analysis",
    "basePrice": 49.99,
    "currency": "USD",
    "addons": [
      { "paddleProductId": "pro_02...", "code": "EXPEDITED",    "price": 20.00, ... },
      { "paddleProductId": "pro_03...", "code": "MERGE_RAW",    "price": 40.00, ... }
      // Y_HAPLOGROUP is filtered out client-side via HIDDEN_STOREFRONT_ADDON_CODES
    ]
  }
]
```

---

## End-to-end example: G25 + addons

| Paddle Product | Price (one-time) | `custom_data` |
|---|---|---|
| G25 Ancestry Analysis | $29.99 | `{ "kind": "service", "service_type": "g25" }` |
| G25 Admixture | $15.00 | `{ "kind": "addon", "parent_service_type": "g25", "addon_code": "G25_ADMIXTURE" }` |
| G25 PCA Analysis | $15.00 | `{ "kind": "addon", "parent_service_type": "g25", "addon_code": "G25_PCA" }` |

---

## Common mistakes

| ❌ Wrong | ✅ Right | Why |
|---|---|---|
| `{ "type": "service", ... }` | `{ "kind": "service", ... }` | The key is `kind`, not `type` (Paddle reserves `type` on the product itself). |
| `{ "kind": "service", "service_type": "QPADM" }` | `{ "kind": "service", "service_type": "qpAdm" }` | The sync uses `Enum.TryParse(..., ignoreCase: true)`, so casing is technically forgiving — but stick to the exact enum spelling for clarity (`qpAdm`, `g25`). |
| `{ "kind": "addon", "service_type": "qpAdm", ... }` | `{ "kind": "addon", "parent_service_type": "qpAdm", ... }` | Addons use `parent_service_type`. `service_type` is for services only and would leave the addon orphan in the catalog. |
| `{ "kind": "addon", "parent_service_type": "qpAdm" }` (no `addon_code`) | `{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "EXPEDITED" }` | Without `addon_code`, fulfillment flags (`ExpeditedProcessing`, `IncludesYHaplogroup`, `IncludesRawMerge`) won't fire when the addon is on an order. |
| `{ "kind": "addon", "parent_service_type": "qpadm", ... }` | `{ "kind": "addon", "parent_service_type": "qpAdm", ... }` | Lower-case `qpadm` *parses* (case-insensitive enum match), but use the exact enum spelling. |
| Service product without an active price | Service product **with** at least one active price | The catalog endpoint silently skips service products that have no active price, so the storefront will say "No qpAdm product is configured." |

If a discriminator is wrong or missing, the row still mirrors into `paddle_products` — just with `NULL` in the affected column. Re-edit the Paddle product, save, and re-sync.

---

## Updating `custom_data`

`custom_data` is editable in the Paddle dashboard at any time (Catalog → product → Edit → Custom data). When you change it:

1. Save in the Paddle dashboard.
2. Run `POST /api/admin/paddle/sync/products` (Admin Jobs → Paddle Sync → Sync). The sync upserts based on Paddle product id, so existing rows are updated in place.

Long-term, the `product.updated` webhook can drive an automatic re-sync; until that handler is wired, manual re-sync is the path.

---

## Adding extra fields

You can put anything else in `custom_data`. The sync mirrors the full object verbatim into `paddle_products.CustomData` (jsonb) without touching the unknown keys. Examples:

```json
{
  "kind": "service",
  "service_type": "qpAdm",
  "feature_tier": "premium",
  "internal_notes": "Bundle launched 2026-Q2",
  "limits": { "max_inspections_per_order": 1 }
}
```

The four standard keys still resolve as before; the extras are reachable from C# via `paddle_products.CustomData` if you ever need them — e.g. with `JsonDocument.Parse(entity.CustomData)`. Just don't rename the four standard keys, since the sync looks them up by name.

---

## What about prices?

**Prices do not need `custom_data`.** The sync doesn't read it. The four columns above all live on the product, not the price.

A price needs:

- An amount in the **major unit** (e.g. `49.99` — Paddle stores it internally as a smallest-unit string)
- A currency (USD, EUR, …)
- A type — choose **One-time** for the order flows; only choose recurring if you actually want a subscription
- Status: active (the dashboard default)

That's it. After creating the price, re-run product sync.

If you do put `custom_data` on a price, it's mirrored to `paddle_prices.CustomData` (jsonb) and ignored by everything in the app.

---

## Cheatsheet

```jsonc
// Service products (one per ServiceType enum value)
{ "kind": "service", "service_type": "qpAdm" }
{ "kind": "service", "service_type": "g25" }

// Addon products (one per addon, attached to one service)
{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "EXPEDITED" }
{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "Y_HAPLOGROUP" }
{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "MERGE_RAW" }
{ "kind": "addon", "parent_service_type": "g25",   "addon_code": "G25_ADMIXTURE" }
{ "kind": "addon", "parent_service_type": "g25",   "addon_code": "G25_PCA" }
```
