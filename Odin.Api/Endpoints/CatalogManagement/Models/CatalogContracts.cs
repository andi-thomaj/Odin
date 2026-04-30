using System.ComponentModel.DataAnnotations;
using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.CatalogManagement.Models;

public class GetCatalogProductContract
{
    public class AddonResponse
    {
        /// <summary>Paddle product id (prefixed <c>pro_</c>) — pass to the order/preview-price endpoints.</summary>
        public required string PaddleProductId { get; set; }

        /// <summary>Paddle price id used at checkout (prefixed <c>pri_</c>).</summary>
        public required string PaddlePriceId { get; set; }

        /// <summary>Stable code from <c>custom_data.addon_code</c> (e.g. <c>EXPEDITED</c>).</summary>
        public required string Code { get; set; }

        public required string DisplayName { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public required string Currency { get; set; }
    }

    public class ProductResponse
    {
        public required string ServiceType { get; set; }
        public required string PaddleProductId { get; set; }
        public required string PaddlePriceId { get; set; }
        public required string DisplayName { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal BasePrice { get; set; }
        public required string Currency { get; set; }
        public List<AddonResponse> Addons { get; set; } = [];
    }
}

public class PreviewOrderPriceContract
{
    public class Request : IValidatableObject
    {
        public ServiceType Service { get; set; } = ServiceType.qpAdm;

        /// <summary>
        /// Paddle product ids (prefixed <c>pro_</c>) for any selected addons. Returned per addon
        /// from <c>GET /api/catalog/products</c>.
        /// </summary>
        public List<string> AddonPaddleProductIds { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Enum.IsDefined(Service))
                yield return new ValidationResult("Invalid service type.", [nameof(Service)]);

            foreach (var id in AddonPaddleProductIds)
            {
                if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
                {
                    yield return new ValidationResult("Each addon Paddle product id must be a non-empty string up to 64 chars.",
                        [nameof(AddonPaddleProductIds)]);
                    break;
                }
            }
        }
    }

    public class AddonLineResponse
    {
        public required string PaddleProductId { get; set; }
        public required string AddonCode { get; set; }
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
