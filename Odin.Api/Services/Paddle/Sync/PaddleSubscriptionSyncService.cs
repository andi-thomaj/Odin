using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Services.Paddle.Models.Subscriptions;
using Odin.Api.Services.Paddle.Resources;

namespace Odin.Api.Services.Paddle.Sync;

public interface IPaddleSubscriptionSyncService
{
    Task<PaddleSyncResult> SyncAllAsync(CancellationToken cancellationToken = default);
    Task<PaddleSyncResult> SyncOneAsync(string paddleSubscriptionId, CancellationToken cancellationToken = default);
}

public sealed class PaddleSubscriptionSyncService(
    IPaddleSubscriptionsResource subscriptionsResource,
    ApplicationDbContext dbContext,
    ILogger<PaddleSubscriptionSyncService> logger) : IPaddleSubscriptionSyncService
{
    public async Task<PaddleSyncResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new PaddleSyncResult { Resource = "subscriptions" };
        var existing = await dbContext.PaddleSubscriptions
            .ToDictionaryAsync(s => s.PaddleSubscriptionId, cancellationToken);

        await foreach (var dto in subscriptionsResource.ListAllAsync(
            new PaddleSubscriptionListQuery { PerPage = 200 }, cancellationToken))
        {
            try
            {
                Upsert(dto, existing, result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Paddle subscription sync: failed for {SubscriptionId}.", dto.Id);
                result.Failed++;
                result.Errors.Add($"{dto.Id}: {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<PaddleSyncResult> SyncOneAsync(string paddleSubscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paddleSubscriptionId);

        var result = new PaddleSyncResult { Resource = "subscriptions" };
        var dto = await subscriptionsResource.GetAsync(paddleSubscriptionId, cancellationToken);

        var existing = await dbContext.PaddleSubscriptions
            .Where(s => s.PaddleSubscriptionId == paddleSubscriptionId)
            .ToDictionaryAsync(s => s.PaddleSubscriptionId, cancellationToken);

        Upsert(dto, existing, result);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private void Upsert(
        PaddleSubscriptionDto dto,
        Dictionary<string, PaddleSubscription> existing,
        PaddleSyncResult result)
    {
        var now = DateTime.UtcNow;
        var raw = JsonSerializer.Serialize(dto, PaddleJson.Options);
        var customData = dto.CustomData?.GetRawText();

        if (existing.TryGetValue(dto.Id, out var entity))
        {
            ApplyFields(entity, dto, raw, customData, now);
            result.Updated++;
        }
        else
        {
            entity = new PaddleSubscription
            {
                PaddleSubscriptionId = dto.Id,
                PaddleCustomerId = dto.CustomerId,
                Status = dto.Status,
                CurrencyCode = dto.CurrencyCode,
                RawJson = raw,
            };
            ApplyFields(entity, dto, raw, customData, now);
            dbContext.PaddleSubscriptions.Add(entity);
            result.Inserted++;
        }
    }

    private static void ApplyFields(PaddleSubscription entity, PaddleSubscriptionDto dto, string raw, string? customData, DateTime now)
    {
        entity.PaddleCustomerId = dto.CustomerId;
        entity.Status = dto.Status;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.CollectionMode = dto.CollectionMode;
        entity.StartedAt = dto.StartedAt;
        entity.FirstBilledAt = dto.FirstBilledAt;
        entity.NextBilledAt = dto.NextBilledAt;
        entity.PausedAt = dto.PausedAt;
        entity.CanceledAt = dto.CanceledAt;
        entity.CurrentPeriodStartsAt = dto.CurrentBillingPeriod?.StartsAt;
        entity.CurrentPeriodEndsAt = dto.CurrentBillingPeriod?.EndsAt;
        entity.ScheduledChangeAction = dto.ScheduledChange?.Action;
        entity.ScheduledChangeEffectiveAt = dto.ScheduledChange?.EffectiveAt;
        entity.RawJson = raw;
        entity.CustomData = customData;
        entity.PaddleCreatedAt = dto.CreatedAt;
        entity.PaddleUpdatedAt = dto.UpdatedAt;
        entity.LastSyncedAt = now;
    }
}
