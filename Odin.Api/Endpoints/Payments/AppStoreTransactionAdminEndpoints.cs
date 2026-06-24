using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.Payments.Models;

namespace Odin.Api.Endpoints.Payments
{
    /// <summary>
    /// Admin read-only view of recorded Apple StoreKit transactions (back-office reconciliation, e.g. spotting
    /// refunds). App-scoped like the admin order list — each app's admin sees that app's transactions.
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

        private static async Task<IResult> GetAll(ApplicationDbContext dbContext)
        {
            // Left-join application_users on CreatedBy → IdentityId so transactions whose creator was never
            // provisioned still appear (with null owner). Materialize first, then map — the enum→string
            // conversions (Service/Status) are done in memory to avoid EF translation edge cases.
            var rows = await (
                from t in dbContext.AppStoreTransactions.AsNoTracking()
                join u in dbContext.Users.AsNoTracking() on t.CreatedBy equals u.IdentityId into owners
                from owner in owners.DefaultIfEmpty()
                orderby t.CreatedAt descending
                select new { Txn = t, Owner = owner }).ToListAsync();

            var response = rows.Select(x => new AdminAppStoreTransactionContract.Response
            {
                Id = x.Txn.Id,
                TransactionId = x.Txn.TransactionId,
                OriginalTransactionId = x.Txn.OriginalTransactionId,
                ProductId = x.Txn.ProductId,
                Service = x.Txn.Service.ToString(),
                Status = x.Txn.Status.ToString(),
                QpadmOrderId = x.Txn.QpadmOrderId,
                G25OrderId = x.Txn.G25OrderId,
                PurchaseDate = x.Txn.PurchaseDate,
                Environment = x.Txn.Environment,
                CreatedAt = x.Txn.CreatedAt,
                CreatedBy = x.Txn.CreatedBy,
                OwnerId = x.Owner?.Id,
                OwnerEmail = x.Owner?.Email,
                OwnerFirstName = x.Owner?.FirstName ?? string.Empty,
                OwnerLastName = x.Owner?.LastName ?? string.Empty,
            }).ToList();

            return Results.Ok(response);
        }
    }
}
