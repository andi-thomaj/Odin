using System.ComponentModel.DataAnnotations;
using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.CatalogManagement.Models;

public class GetCatalogProductContract
{
    public class AddonResponse
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        public required string DisplayName { get; set; }
        public decimal Price { get; set; }
    }

    public class ProductResponse
    {
        public required string ServiceType { get; set; }
        public required string DisplayName { get; set; }
        public string? Description { get; set; }
        public decimal BasePrice { get; set; }
        public List<AddonResponse> Addons { get; set; } = [];
    }
}

public class PreviewOrderPriceContract
{
    public class Request : IValidatableObject
    {
        public ServiceType Service { get; set; } = ServiceType.qpAdm;
        public List<int> AddonIds { get; set; } = [];
        public string? PromoCode { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Enum.IsDefined(Service))
                yield return new ValidationResult("Invalid service type.", [nameof(Service)]);

            if (PromoCode is { Length: > 64 })
                yield return new ValidationResult("Promo code is too long.", [nameof(PromoCode)]);
        }
    }

    public class AddonLineResponse
    {
        public int ProductAddonId { get; set; }
        public required string Code { get; set; }
        public required string DisplayName { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class Response
    {
        public decimal BasePrice { get; set; }
        public List<AddonLineResponse> AddonLines { get; set; } = [];
        public decimal SubtotalBeforeDiscount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal Total { get; set; }
    }
}
