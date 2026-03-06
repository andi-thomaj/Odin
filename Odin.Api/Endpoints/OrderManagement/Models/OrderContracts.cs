using System.ComponentModel.DataAnnotations;
using Odin.Api.Data.Enums;
using OrderServiceEnum = Odin.Api.Data.Enums.OrderService;

namespace Odin.Api.Endpoints.OrderManagement.Models
{
    public class CreateOrderContract
    {
        public class Request : IValidatableObject
        {
            public required decimal Price { get; set; }
            public required OrderServiceEnum Service { get; set; }
            public required int GeneticInspectionId { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (Price <= 0)
                {
                    yield return new ValidationResult("Price must be greater than zero.", [nameof(Price)]);
                }

                if (!Enum.IsDefined(Service))
                {
                    yield return new ValidationResult("Invalid service type.", [nameof(Service)]);
                }

                if (GeneticInspectionId <= 0)
                {
                    yield return new ValidationResult("Genetic inspection ID is required.", [nameof(GeneticInspectionId)]);
                }
            }
        }

        public class Response
        {
            public int Id { get; set; }
            public decimal Price { get; set; }
            public string Service { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int GeneticInspectionId { get; set; }
        }
    }

    public class UpdateOrderContract
    {
        public class Request : IValidatableObject
        {
            public required decimal Price { get; set; }
            public required OrderServiceEnum Service { get; set; }
            public required OrderStatus Status { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (Price <= 0)
                {
                    yield return new ValidationResult("Price must be greater than zero.", [nameof(Price)]);
                }

                if (!Enum.IsDefined(Service))
                {
                    yield return new ValidationResult("Invalid service type.", [nameof(Service)]);
                }

                if (!Enum.IsDefined(Status))
                {
                    yield return new ValidationResult("Invalid status.", [nameof(Status)]);
                }
            }
        }
    }

    public class GetOrderContract
    {
        public class Response
        {
            public int Id { get; set; }
            public decimal Price { get; set; }
            public string Service { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int GeneticInspectionId { get; set; }
            public DateTime CreatedAt { get; set; }
            public string CreatedBy { get; set; } = string.Empty;
            public DateTime UpdatedAt { get; set; }
            public string UpdatedBy { get; set; } = string.Empty;
        }
    }
}
