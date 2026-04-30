# Paddle Catalog Setup

This guide walks through wiring up Paddle as the source of truth for the product catalog: how to create products in the Paddle dashboard so the local sync picks them up correctly, how to run the sync, and how to verify the result.

For a focused deep-dive on the `custom_data` shape (more examples, common mistakes, FAQ on prices), see [paddle-custom-data.md](paddle-custom-data.md).

The integration uses an **anti-corruption layer** — Paddle owns billing-relevant data (names, prices, tax category, status), and a small set of discriminator columns we project from `custom_data` lets the rest of the app run pure SQL queries without parsing JSON on every read.

---

## 1. Prerequisites

### API keys & config

Set these via `dotnet user-secrets` on `Odin.Api` (do **not** commit them to `appsettings.json`):

```bash
cd Odin/Odin.Api
dotnet user-secrets set "Paddle:ApiKey"        "pdl_sdbx_apikey_..."        # or pdl_live_apikey_...
dotnet user-secrets set "Paddle:WebhookSecret" "pdl_ntfset_..."             # from the notification setting
dotnet user-secrets set "Paddle:ApiBaseUrl"    "https://sandbox-api.paddle.com"   # https://api.paddle.com in prod
```

Optional tuning (defaults shown):

| Key | Default | Notes |
|-----|---------|-------|
| `Paddle:ApiVersion` | `1` | Sent as `Paddle-Version` header on every call. |
| `Paddle:RequestTimeoutSeconds` | `30` | Per-request HTTP timeout. |
| `Paddle:MaxRetries` | `3` | Total attempts = `1 + MaxRetries` (5xx/429 only; honours `Retry-After`). |

### Webhook destination

In **Paddle Dashboard → Developer Tools → Notifications → Add destination**:

- **URL:** `https://YOUR_API_HOST/webhooks/paddle`
- **Notifications:** at minimum, subscribe to `transaction.completed`, `transaction.refunded`, `product.created`, `product.updated`, `price.created`, `price.updated`. Add subscription events if you sell subscriptions.
- Copy the **secret key** into `Paddle:WebhookSecret`. This is what `PaddleWebhookSignatureVerifier` uses to validate `Paddle-Signature`.

---

## 2. Create products in the Paddle dashboard

In **Paddle Dashboard → Catalog → Products → New product** (sandbox first; replicate to live when ready), create one product per service and one product per addon. The shape of each product's **`custom_data`** is what the sync uses to resolve discriminator columns.

> The `custom_data` JSON editor is in the **"Custom data"** section of the product form.

### 2.1 Service products

One product per service. The `service_type` must match a value of the `ServiceType` enum (`qpAdm` or `g25`).

**qpAdm service** — `Name: qpAdm Ancestry Analysis`

```json
{
  "kind": "service",
  "service_type": "qpAdm"
}
```

**G25 service** — `Name: G25 Ancestry Analysis`

```json
{
  "kind": "service",
  "service_type": "g25"
}
```

For each service product, add a **price**: pick a currency, set unit price (e.g. `$49.99`), and set the price to **One-time** (or recurring if your service is subscription-based — Paddle handles both).

### 2.2 Addon products

One product per addon. `addon_code` must match a code that order-fulfillment logic checks (`EXPEDITED`, `Y_HAPLOGROUP`, `MERGE_RAW`, …). `parent_service_type` decides which service the addon attaches to in `/api/catalog/products`.

**Expedited processing (qpAdm)** — `Name: Expedited Processing`

```json
{
  "kind": "addon",
  "parent_service_type": "qpAdm",
  "addon_code": "EXPEDITED"
}
```

**Y haplogroup (qpAdm)** — `Name: Y Haplogroup`

```json
{
  "kind": "addon",
  "parent_service_type": "qpAdm",
  "addon_code": "Y_HAPLOGROUP"
}
```

**Raw merge (qpAdm)** — `Name: Merge Raw Data`

```json
{
  "kind": "addon",
  "parent_service_type": "qpAdm",
  "addon_code": "MERGE_RAW"
}
```

**G25 admixture addon** — `Name: G25 Admixture`

```json
{
  "kind": "addon",
  "parent_service_type": "g25",
  "addon_code": "G25_ADMIXTURE"
}
```

Each addon product also needs at least one **active price**.

### 2.3 Field reference

| Field | Required for | Maps to | Notes |
|-------|--------------|---------|-------|
| `kind` | every product | `paddle_products.Kind` | `"service"` or `"addon"`. Anything else is ignored by sync but the row is still mirrored. |
| `service_type` | services | `paddle_products.ServiceType` | Must match a `ServiceType` enum value (`qpAdm`, `g25`). Case-insensitive in sync but stick to the enum spelling for clarity. |
| `parent_service_type` | addons | `paddle_products.ParentServiceType` | Same enum domain. Determines which service the addon shows up under in `/api/catalog/products`. |
| `addon_code` | addons | `paddle_products.AddonCode` | Stable identifier used by order-fulfillment flag detection (`ExpeditedProcessing`, `IncludesYHaplogroup`, `IncludesRawMerge`). |

Anything else you put in `custom_data` is preserved verbatim in `paddle_products.CustomData` (jsonb) — useful for future extensions without a schema change.

---

## 3. Apply the EF migration

The migration adds the Paddle mirror tables and drops the old local catalog tables. Run once per environment:

```bash
cd Odin
dotnet ef database update --project Odin.Api/Odin.Api.csproj --startup-project Odin.Api/Odin.Api.csproj
```

What the latest migration (`20260430174938_SwitchToPaddleCatalog`) does:

- **Creates:** `paddle_products`, `paddle_prices`, `paddle_customers`, `paddle_subscriptions`, `paddle_transactions`, `paddle_notifications`.
- **Adds:** `qpadm_orders.AddonsJson` (jsonb snapshot of which addons were on the order).
- **Drops:** `catalog_products`, `catalog_product_addons`, `product_addons`, `order_line_addons`, `promo_codes` and their FK from `qpadm_orders`.

After this runs, `paddle_products` and `paddle_prices` are empty until you sync.

---

## 4. Run the initial sync

Sync pulls every product (with prices via `?include=prices`) from Paddle and upserts into the local mirror. Discriminator columns are projected from each product's `custom_data` during the upsert.

The sync endpoint is **AdminOnly** — caller must hold the `Admin` app role and a verified email.

```bash
# Sync all products + prices
curl -X POST https://YOUR_API_HOST/api/admin/paddle/sync/products \
  -H "Authorization: Bearer YOUR_AUTH0_ACCESS_TOKEN"
```

Example response:

```json
{
  "resource": "products",
  "inserted": 6,
  "updated": 0,
  "skipped": 0,
  "failed": 0,
  "total": 6,
  "errors": []
}
```

Re-run any time you change a product or price in Paddle to refresh the local mirror. (Long-term: a webhook-driven sync on `product.updated`/`price.updated` removes the need for manual re-sync.)

You can also re-sync a single product:

```bash
curl -X POST https://YOUR_API_HOST/api/admin/paddle/sync/products/pro_01abc... \
  -H "Authorization: Bearer YOUR_AUTH0_ACCESS_TOKEN"
```

Other resources (also Admin-only): `sync/customers`, `sync/subscriptions`, `sync/transactions`, plus per-id variants.

---

## 5. Verify the result

### 5.1 Public catalog endpoint

`GET /api/catalog/products` (requires email-verified user) should now return your services + addons. Example:

```json
[
  {
    "serviceType": "qpAdm",
    "paddleProductId": "pro_01abc...",
    "paddlePriceId": "pri_01abc...",
    "displayName": "qpAdm Ancestry Analysis",
    "description": "Deep ancestry modeling with reference populations.",
    "imageUrl": null,
    "basePrice": 49.99,
    "currency": "USD",
    "addons": [
      {
        "paddleProductId": "pro_01def...",
        "paddlePriceId": "pri_01def...",
        "code": "EXPEDITED",
        "displayName": "Expedited Processing",
        "description": null,
        "price": 20.00,
        "currency": "USD"
      }
    ]
  }
]
```

The frontend uses `paddleProductId` / `paddlePriceId` to populate Paddle.js checkout, and passes addon `paddleProductId` values back when posting an order:

```jsonc
// POST /api/orders  (multipart form; relevant fields shown)
{
  "service": "qpAdm",
  "addonPaddleProductIds": ["pro_01def...", "pro_01ghi..."]
}
```

### 5.2 Spot-check the database

```sql
-- Services and their resolved enum
SELECT "PaddleProductId", "Name", "Kind", "ServiceType", "Status"
FROM   paddle_products
WHERE  "Kind" = 'service';

-- Addons and what service they attach to
SELECT "PaddleProductId", "Name", "Kind", "AddonCode", "ParentServiceType", "Status"
FROM   paddle_products
WHERE  "Kind" = 'addon';

-- Active prices linked to each Paddle product
SELECT pp."PaddleProductId", pp."Name", pr."PaddlePriceId", pr."UnitPriceAmount", pr."UnitPriceCurrency"
FROM   paddle_products pp
JOIN   paddle_prices    pr ON pr."PaddleProductInternalId" = pp."Id"
WHERE  pp."Status" = 'active' AND pr."Status" = 'active'
ORDER  BY pp."Kind", pp."Name";
```

If `ServiceType` is `NULL` on a service product, your `custom_data.service_type` either wasn't set or doesn't match the enum (`qpAdm` / `g25`) — fix in the dashboard and re-sync.

---

## 6. Webhook + replay

Once products are synced, every transaction/subscription event Paddle delivers is recorded in `paddle_notifications` keyed on `notification_id` (idempotent). You can list, inspect, and replay them through the admin endpoints:

```bash
# Last 100 webhook events
curl -X GET https://YOUR_API_HOST/api/admin/paddle/notifications \
  -H "Authorization: Bearer YOUR_AUTH0_ACCESS_TOKEN"

# Single event including raw payload
curl -X GET https://YOUR_API_HOST/api/admin/paddle/notifications/42 \
  -H "Authorization: Bearer YOUR_AUTH0_ACCESS_TOKEN"

# Ask Paddle to redeliver the original event (only event-origin under 90 days old)
curl -X POST https://YOUR_API_HOST/api/admin/paddle/notifications/42/replay \
  -H "Authorization: Bearer YOUR_AUTH0_ACCESS_TOKEN"

# Pull missing notifications from Paddle into our log
curl -X POST https://YOUR_API_HOST/api/admin/paddle/notifications/backfill \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_AUTH0_ACCESS_TOKEN" \
  -d '{"from":"2026-04-25T00:00:00Z","to":"2026-04-30T23:59:59Z"}'
```

Replay is the recovery mechanism when projection logic changes after deploy: Paddle re-delivers the original event through the same `/webhooks/paddle` endpoint, the new handler runs, and the `paddle_notifications` row's `ProcessedStatus` flips back to `processed`.

---

## 7. Common issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| Sync inserts the row but `ServiceType` / `Kind` are `NULL` | `custom_data` typo or wrong key | Fix `custom_data` in the dashboard, re-run `POST /api/admin/paddle/sync/products`. |
| `OrderPricingService` throws `"No active Paddle product is linked to the selected service."` | Service product missing, archived, or `ServiceType` wasn't projected | Check `SELECT ... FROM paddle_products WHERE "Kind"='service'` — if missing, add `custom_data.service_type` and re-sync. |
| Addon shows up in Paddle but not in `/api/catalog/products` under the right service | `parent_service_type` typo or absent | Fix `custom_data.parent_service_type` and re-sync. |
| Webhook returns 401 `Unauthorized` | `Paddle:WebhookSecret` empty or wrong destination key | Re-copy the secret from the Paddle notification setting into user-secrets and restart. |
| Webhook returns 503 | `Paddle:WebhookSecret` not configured | Set the secret. The handler refuses to validate without it (fail-closed). |
| Paddle replay returns 422 `notification_origin_invalid` | The notification was already a replay, or older than 90 days | Replays of replays are not allowed. Pull a fresh event or wait for the next live one. |
| Build error after changing prices in Paddle | Migrations don't change | None needed — pricing changes are pure data; just re-sync. |

---

## 8. Quick reference — discriminator cheatsheet

```jsonc
// Service product (one per ServiceType enum value)
{ "kind": "service", "service_type": "qpAdm" }
{ "kind": "service", "service_type": "g25" }

// Addon products (one per addon, attached to one service)
{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "EXPEDITED" }
{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "Y_HAPLOGROUP" }
{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "MERGE_RAW" }
{ "kind": "addon", "parent_service_type": "g25",   "addon_code": "G25_ADMIXTURE" }
{ "kind": "addon", "parent_service_type": "g25",   "addon_code": "G25_PCA" }
```

Anything not in this cheatsheet is fine to add to `custom_data` — it gets mirrored to `paddle_products.CustomData` (jsonb) and ignored by the discriminator projection.
