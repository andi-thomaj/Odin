using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Data;
using Odin.Api.Endpoints.Payments.Models;

namespace Odin.Api.Endpoints.Payments
{
    /// <summary>
    /// Admin read-only feed of EVERY recorded Apple in-app purchase, for back-office reconciliation: the paid
    /// qpAdm/G25 analysis orders (<c>app_store_transactions</c>) plus the per-order add-ons — the Y-DNA results
    /// unlock (<c>qpadm_ydna_unlocks</c>) and the "Through the Ages" AI portraits (<c>ancestral_portrait_sets</c>).
    /// Each row carries the owning user, the linked order, and the nominal money paid (from <see cref="AppleIapOptions"/>).
    /// </summary>
    public static class AppStoreTransactionAdminEndpoints
    {
        public static void MapAppStoreTransactionAdminEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("api/admin/app-store-transactions", GetAll)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("authenticated")
                .Produces<IEnumerable<AdminAppStoreTransactionContract.Response>>(StatusCodes.Status200OK);
        }

        private static async Task<IResult> GetAll(
            ApplicationDbContext dbContext,
            IOptions<AppleIapOptions> appleIapOptions)
        {
            var opts = appleIapOptions.Value;
            var currency = opts.Currency;

            // Load every purchase source, then resolve owners for just the creators referenced (not all users) —
            // a left join so a purchase whose creator was never provisioned still appears (with a null owner).
            var txns = await dbContext.AppStoreTransactions.AsNoTracking().ToListAsync();
            var ydnaUnlocks = await dbContext.QpadmYDnaUnlocks.AsNoTracking().ToListAsync();
            var portraitSets = await dbContext.AncestralPortraitSets.AsNoTracking()
                .Select(s => new { s.Id, s.OrderId, s.TransactionId, s.CreatedAt, s.CreatedBy, s.RefundedAt })
                .ToListAsync();

            var creatorSubs = txns.Select(t => t.CreatedBy)
                .Concat(ydnaUnlocks.Select(u => u.CreatedBy))
                .Concat(portraitSets.Select(s => s.CreatedBy))
                .Where(sub => !string.IsNullOrEmpty(sub))
                .Distinct()
                .ToList();

            var ownersByIdentity = (await dbContext.Users.AsNoTracking()
                    .Where(u => creatorSubs.Contains(u.IdentityId))
                    .Select(u => new OwnerInfo(u.Id, u.IdentityId, u.Email, u.FirstName, u.LastName))
                    .ToListAsync())
                .GroupBy(o => o.IdentityId)
                .ToDictionary(g => g.Key, g => g.First());

            var rows = new List<AdminAppStoreTransactionContract.Response>();

            // 1. Paid qpAdm/G25 analysis orders (the real StoreKit transaction rows).
            foreach (var t in txns)
            {
                var isG25 = t.Service == Data.Enums.ServiceType.g25;
                var row = new AdminAppStoreTransactionContract.Response
                {
                    Id = t.Id,
                    RowKey = $"order:{t.Id}",
                    Kind = "Order",
                    ProductLabel = isG25 ? "G25 Analysis" : "qpAdm Analysis",
                    Amount = isG25 ? opts.G25Price : opts.QpadmPrice,
                    Currency = currency,
                    TransactionId = t.TransactionId,
                    OriginalTransactionId = t.OriginalTransactionId,
                    ProductId = t.ProductId,
                    Service = t.Service.ToString(),
                    Status = t.Status.ToString(),
                    QpadmOrderId = t.QpadmOrderId,
                    G25OrderId = t.G25OrderId,
                    PurchaseDate = t.PurchaseDate,
                    Environment = t.Environment,
                    CreatedAt = t.CreatedAt,
                    CreatedBy = t.CreatedBy,
                };
                ApplyOwner(row, t.CreatedBy, ownersByIdentity);
                rows.Add(row);
            }

            // 2. Y-DNA results unlock add-on (separate entitlement table — no Apple env/product persisted on the row,
            //    a refund DELETES the row, so a present row is a live "Consumed" purchase).
            foreach (var u in ydnaUnlocks)
            {
                var row = new AdminAppStoreTransactionContract.Response
                {
                    Id = u.Id,
                    RowKey = $"ydna:{u.Id}",
                    Kind = "YDnaUnlock",
                    ProductLabel = "Y-DNA Unlock",
                    Amount = opts.YDnaPrice,
                    Currency = currency,
                    TransactionId = u.TransactionId,
                    OriginalTransactionId = string.Empty,
                    ProductId = opts.YDnaProductId,
                    Service = Data.Enums.ServiceType.qpAdm.ToString(),
                    Status = Data.Enums.AppStoreTransactionStatus.Consumed.ToString(),
                    QpadmOrderId = u.OrderId,
                    G25OrderId = null,
                    PurchaseDate = u.CreatedAt,
                    Environment = string.Empty,
                    CreatedAt = u.CreatedAt,
                    CreatedBy = u.CreatedBy,
                };
                ApplyOwner(row, u.CreatedBy, ownersByIdentity);
                rows.Add(row);
            }

            // 3. "Through the Ages" AI ancestral-portraits add-on (Guid-keyed; an order can have many iterations).
            foreach (var s in portraitSets)
            {
                var row = new AdminAppStoreTransactionContract.Response
                {
                    Id = 0, // Guid-keyed source — RowKey is the stable key.
                    RowKey = $"aiportraits:{s.Id}",
                    Kind = "AiPortraits",
                    ProductLabel = "Through the Ages",
                    Amount = opts.AiPortraitsPrice,
                    Currency = currency,
                    TransactionId = s.TransactionId,
                    OriginalTransactionId = string.Empty,
                    ProductId = opts.AiPortraitsProductId,
                    Service = Data.Enums.ServiceType.qpAdm.ToString(),
                    // Refund is tracked via RefundedAt (the set is kept so the images aren't destroyed); a present
                    // row with no RefundedAt is a live "Consumed" purchase.
                    Status = (s.RefundedAt is null
                        ? Data.Enums.AppStoreTransactionStatus.Consumed
                        : Data.Enums.AppStoreTransactionStatus.Refunded).ToString(),
                    QpadmOrderId = s.OrderId,
                    G25OrderId = null,
                    PurchaseDate = s.CreatedAt,
                    Environment = string.Empty,
                    CreatedAt = s.CreatedAt,
                    CreatedBy = s.CreatedBy,
                };
                ApplyOwner(row, s.CreatedBy, ownersByIdentity);
                rows.Add(row);
            }

            // Newest purchase first across every kind.
            var response = rows.OrderByDescending(r => r.CreatedAt).ToList();
            return Results.Ok(response);
        }

        private static void ApplyOwner(
            AdminAppStoreTransactionContract.Response row,
            string createdBy,
            IReadOnlyDictionary<string, OwnerInfo> ownersByIdentity)
        {
            if (createdBy is { Length: > 0 } && ownersByIdentity.TryGetValue(createdBy, out var owner))
            {
                row.OwnerId = owner.Id;
                row.OwnerEmail = owner.Email;
                row.OwnerFirstName = owner.FirstName ?? string.Empty;
                row.OwnerLastName = owner.LastName ?? string.Empty;
            }
        }

        /// <summary>Minimal owning-user projection for the in-memory creator-to-user resolution.</summary>
        private sealed record OwnerInfo(int Id, string IdentityId, string? Email, string? FirstName, string? LastName);
    }
}
