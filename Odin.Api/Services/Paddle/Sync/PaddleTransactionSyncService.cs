using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Services.Paddle.Models.Transactions;
using Odin.Api.Services.Paddle.Resources;

namespace Odin.Api.Services.Paddle.Sync;

public interface IPaddleTransactionSyncService
{
    /// <summary>Pulls every transaction since <paramref name="billedAtFrom"/> (or the beginning of time when null) and upserts it.</summary>
    Task<PaddleSyncResult> SyncAllAsync(DateTimeOffset? billedAtFrom = null, CancellationToken cancellationToken = default);
    Task<PaddleSyncResult> SyncOneAsync(string paddleTransactionId, CancellationToken cancellationToken = default);
}

public sealed class PaddleTransactionSyncService(
    IPaddleTransactionsResource transactionsResource,
    ApplicationDbContext dbContext,
    ILogger<PaddleTransactionSyncService> logger) : IPaddleTransactionSyncService
{
    public async Task<PaddleSyncResult> SyncAllAsync(DateTimeOffset? billedAtFrom = null, CancellationToken cancellationToken = default)
    {
        var result = new PaddleSyncResult { Resource = "transactions" };
        var query = new PaddleTransactionListQuery
        {
            // Paddle caps transactions at 30 per page.
            PerPage = 30,
            BilledAtFrom = billedAtFrom,
            OrderBy = "billed_at[ASC]",
        };

        var existingIds = await dbContext.PaddleTransactions
            .Select(t => t.PaddleTransactionId)
            .ToListAsync(cancellationToken);
        var existingSet = existingIds.ToHashSet(StringComparer.Ordinal);

        await foreach (var dto in transactionsResource.ListAllAsync(query, cancellationToken))
        {
            try
            {
                await UpsertAsync(dto, existingSet, result, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Paddle transaction sync: failed for {TransactionId}.", dto.Id);
                result.Failed++;
                result.Errors.Add($"{dto.Id}: {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<PaddleSyncResult> SyncOneAsync(string paddleTransactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paddleTransactionId);

        var result = new PaddleSyncResult { Resource = "transactions" };
        var dto = await transactionsResource.GetAsync(paddleTransactionId, cancellationToken: cancellationToken);

        var existingSet = (await dbContext.PaddleTransactions
            .Where(t => t.PaddleTransactionId == paddleTransactionId)
            .Select(t => t.PaddleTransactionId)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        await UpsertAsync(dto, existingSet, result, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task UpsertAsync(
        PaddleTransactionDto dto,
        HashSet<string> existingSet,
        PaddleSyncResult result,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var raw = JsonSerializer.Serialize(dto, PaddleJson.Options);
        var customData = dto.CustomData?.GetRawText();
        var totals = dto.Details?.Totals;

        if (existingSet.Contains(dto.Id))
        {
            var entity = await dbContext.PaddleTransactions
                .FirstAsync(t => t.PaddleTransactionId == dto.Id, cancellationToken);
            ApplyFields(entity, dto, totals, raw, customData, now);
            result.Updated++;
        }
        else
        {
            var entity = new PaddleTransaction
            {
                PaddleTransactionId = dto.Id,
                Status = dto.Status,
                CurrencyCode = dto.CurrencyCode,
                RawJson = raw,
            };
            ApplyFields(entity, dto, totals, raw, customData, now);
            dbContext.PaddleTransactions.Add(entity);
            existingSet.Add(dto.Id);
            result.Inserted++;
        }
    }

    private static void ApplyFields(PaddleTransaction entity, PaddleTransactionDto dto, PaddleTransactionTotals? totals, string raw, string? customData, DateTime now)
    {
        entity.Status = dto.Status;
        entity.PaddleCustomerId = dto.CustomerId;
        entity.PaddleSubscriptionId = dto.SubscriptionId;
        entity.InvoiceId = dto.InvoiceId;
        entity.InvoiceNumber = dto.InvoiceNumber;
        entity.Origin = dto.Origin;
        entity.CollectionMode = dto.CollectionMode;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.Subtotal = totals?.Subtotal;
        entity.TaxTotal = totals?.Tax;
        entity.DiscountTotal = totals?.Discount;
        entity.GrandTotal = totals?.GrandTotal ?? totals?.Total;
        entity.BilledAt = dto.BilledAt;
        entity.PaddleCreatedAt = dto.CreatedAt;
        entity.PaddleUpdatedAt = dto.UpdatedAt;
        entity.RawJson = raw;
        entity.CustomData = customData;
        entity.LastSyncedAt = now;
    }
}
