using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.CalculatorManagement.Models;

namespace Odin.Api.Endpoints.CalculatorManagement;

public interface ICalculatorService
{
    Task<IReadOnlyList<GetCalculatorContract.Response>> GetAdminCalculatorsAsync(CancellationToken ct = default);
    Task<GetCalculatorContract.Response?> GetByIdAsync(int id, int currentUserId, bool currentUserIsAdmin, CancellationToken ct = default);
    Task<(GetCalculatorContract.Response? Response, string? Error)> CreateAsync(CreateCalculatorContract.Request request, int currentUserId, bool currentUserIsAdmin, string identityId, CancellationToken ct = default);
    Task<(GetCalculatorContract.Response? Response, string? Error, bool NotFound, bool Forbidden)> UpdateAsync(int id, UpdateCalculatorContract.Request request, int currentUserId, bool currentUserIsAdmin, string identityId, CancellationToken ct = default);
    Task<(bool Deleted, string? Error, bool Forbidden)> DeleteAsync(int id, int currentUserId, bool currentUserIsAdmin, CancellationToken ct = default);
}

public class CalculatorService(ApplicationDbContext dbContext) : ICalculatorService
{
    public async Task<IReadOnlyList<GetCalculatorContract.Response>> GetAdminCalculatorsAsync(CancellationToken ct = default)
    {
        return await dbContext.Calculators
            .AsNoTracking()
            .Where(c => c.IsAdmin)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Label)
            .Select(c => new GetCalculatorContract.Response
            {
                Id = c.Id,
                Label = c.Label,
                Coordinates = c.Coordinates,
                Type = c.Type,
                IsAdmin = c.IsAdmin,
                UserId = c.UserId,
                UserEmail = c.User.Email,
                UserUsername = c.User.Username
            })
            .ToListAsync(ct);
    }

    public async Task<GetCalculatorContract.Response?> GetByIdAsync(int id, int currentUserId, bool currentUserIsAdmin, CancellationToken ct = default)
    {
        return await dbContext.Calculators
            .AsNoTracking()
            .Where(c => c.Id == id && (currentUserIsAdmin || c.IsAdmin || c.UserId == currentUserId))
            .Select(c => new GetCalculatorContract.Response
            {
                Id = c.Id,
                Label = c.Label,
                Coordinates = c.Coordinates,
                Type = c.Type,
                IsAdmin = c.IsAdmin,
                UserId = c.UserId,
                UserEmail = c.User.Email,
                UserUsername = c.User.Username
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(GetCalculatorContract.Response? Response, string? Error)> CreateAsync(
        CreateCalculatorContract.Request request, int currentUserId, bool currentUserIsAdmin, string identityId, CancellationToken ct = default)
    {
        var (label, coordinates, error) = ValidateBasic(request.Label, request.Coordinates, request.Type);
        if (error is not null) return (null, error);

        var duplicate = await DuplicateExistsAsync(request.Type, label, currentUserId, null, ct);
        if (duplicate is not null) return (null, duplicate);

        var entity = new Calculator
        {
            Label = label,
            Coordinates = coordinates,
            Type = request.Type,
            IsAdmin = currentUserIsAdmin,
            UserId = currentUserId,
            CreatedBy = identityId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Calculators.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, currentUserId, currentUserIsAdmin, ct);
        return (response, null);
    }

    public async Task<(GetCalculatorContract.Response? Response, string? Error, bool NotFound, bool Forbidden)> UpdateAsync(
        int id, UpdateCalculatorContract.Request request, int currentUserId, bool currentUserIsAdmin, string identityId, CancellationToken ct = default)
    {
        var entity = await dbContext.Calculators.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return (null, null, true, false);

        if (!CanModify(entity, currentUserId, currentUserIsAdmin))
            return (null, null, false, true);

        var (label, coordinates, error) = ValidateBasic(request.Label, request.Coordinates, request.Type);
        if (error is not null) return (null, error, false, false);

        var duplicate = await DuplicateExistsAsync(request.Type, label, entity.UserId, id, ct);
        if (duplicate is not null) return (null, duplicate, false, false);

        entity.Label = label;
        entity.Coordinates = coordinates;
        entity.Type = request.Type;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = identityId;
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, currentUserId, currentUserIsAdmin, ct);
        return (response, null, false, false);
    }

    public async Task<(bool Deleted, string? Error, bool Forbidden)> DeleteAsync(int id, int currentUserId, bool currentUserIsAdmin, CancellationToken ct = default)
    {
        var entity = await dbContext.Calculators.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return (false, null, false);

        if (!CanModify(entity, currentUserId, currentUserIsAdmin))
            return (false, null, true);

        dbContext.Calculators.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return (true, null, false);
    }

    private static bool CanModify(Calculator calc, int currentUserId, bool currentUserIsAdmin)
    {
        if (currentUserIsAdmin) return true;
        if (calc.IsAdmin) return false;
        return calc.UserId == currentUserId;
    }

    private static (string Label, string Coordinates, string? Error) ValidateBasic(string label, string coordinates, CalculatorType type)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > 500)
            return (string.Empty, string.Empty, "Label is required and must be 1-500 characters.");

        if (string.IsNullOrWhiteSpace(coordinates))
            return (string.Empty, string.Empty, "Coordinates are required.");

        if (!Enum.IsDefined(type))
            return (string.Empty, string.Empty, "Type must be a valid calculator type.");

        return (label.Trim(), coordinates.Trim(), null);
    }

    private async Task<string?> DuplicateExistsAsync(CalculatorType type, string label, int ownerUserId, int? existingId, CancellationToken ct)
    {
        var exists = await dbContext.Calculators
            .AsNoTracking()
            .AnyAsync(
                c => c.Type == type
                    && c.Label == label
                    && c.UserId == ownerUserId
                    && (existingId == null || c.Id != existingId),
                ct);
        return exists
            ? $"A {type} calculator labeled '{label}' already exists for this user."
            : null;
    }
}
