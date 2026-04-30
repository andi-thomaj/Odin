using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Services.Paddle.Models.Customers;
using Odin.Api.Services.Paddle.Resources;

namespace Odin.Api.Services.Paddle.Sync;

public interface IPaddleCustomerSyncService
{
    Task<PaddleSyncResult> SyncAllAsync(CancellationToken cancellationToken = default);
    Task<PaddleSyncResult> SyncOneAsync(string paddleCustomerId, CancellationToken cancellationToken = default);
}

public sealed class PaddleCustomerSyncService(
    IPaddleCustomersResource customersResource,
    ApplicationDbContext dbContext,
    ILogger<PaddleCustomerSyncService> logger) : IPaddleCustomerSyncService
{
    public async Task<PaddleSyncResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new PaddleSyncResult { Resource = "customers" };

        var existing = await dbContext.PaddleCustomers
            .ToDictionaryAsync(c => c.PaddleCustomerId, cancellationToken);

        await foreach (var dto in customersResource.ListAllAsync(
            new PaddleCustomerListQuery { PerPage = 200 }, cancellationToken))
        {
            try
            {
                Upsert(dto, existing, result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Paddle customer sync: failed for {CustomerId}.", dto.Id);
                result.Failed++;
                result.Errors.Add($"{dto.Id}: {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<PaddleSyncResult> SyncOneAsync(string paddleCustomerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paddleCustomerId);

        var result = new PaddleSyncResult { Resource = "customers" };
        var dto = await customersResource.GetAsync(paddleCustomerId, cancellationToken);
        var existing = await dbContext.PaddleCustomers
            .Where(c => c.PaddleCustomerId == paddleCustomerId)
            .ToDictionaryAsync(c => c.PaddleCustomerId, cancellationToken);

        Upsert(dto, existing, result);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private void Upsert(
        PaddleCustomerDto dto,
        Dictionary<string, PaddleCustomer> existing,
        PaddleSyncResult result)
    {
        var now = DateTime.UtcNow;
        var customData = dto.CustomData?.GetRawText();
        var userId = ExtractUserId(dto);

        if (existing.TryGetValue(dto.Id, out var entity))
        {
            entity.Email = dto.Email;
            entity.Name = dto.Name;
            entity.Locale = dto.Locale;
            entity.MarketingConsent = dto.MarketingConsent;
            entity.Status = dto.Status ?? "active";
            entity.CustomData = customData;
            entity.UserId ??= userId;
            entity.PaddleCreatedAt = dto.CreatedAt;
            entity.PaddleUpdatedAt = dto.UpdatedAt;
            entity.LastSyncedAt = now;
            result.Updated++;
        }
        else
        {
            dbContext.PaddleCustomers.Add(new PaddleCustomer
            {
                PaddleCustomerId = dto.Id,
                Email = dto.Email,
                Name = dto.Name,
                Locale = dto.Locale,
                MarketingConsent = dto.MarketingConsent,
                Status = dto.Status ?? "active",
                CustomData = customData,
                UserId = userId,
                PaddleCreatedAt = dto.CreatedAt,
                PaddleUpdatedAt = dto.UpdatedAt,
                LastSyncedAt = now,
            });
            result.Inserted++;
        }
    }

    private static string? ExtractUserId(PaddleCustomerDto dto)
    {
        if (dto.CustomData is not { ValueKind: System.Text.Json.JsonValueKind.Object } cd)
            return null;
        return cd.TryGetProperty("user_id", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString()
            : null;
    }
}
