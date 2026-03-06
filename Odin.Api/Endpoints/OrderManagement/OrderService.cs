using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.OrderManagement.Models;
using OrderServiceEnum = Odin.Api.Data.Enums.OrderService;

namespace Odin.Api.Endpoints.OrderManagement
{
    public interface IOrderService
    {
        Task<CreateOrderContract.Response> CreateAsync(CreateOrderContract.Request request);
        Task<GetOrderContract.Response?> GetByIdAsync(int id);
        Task<IEnumerable<GetOrderContract.Response>> GetAllAsync();
        Task<GetOrderContract.Response?> UpdateAsync(int id, UpdateOrderContract.Request request);
        Task<bool> DeleteAsync(int id);
    }

    public class OrderService(ApplicationDbContext dbContext) : IOrderService
    {
        public async Task<CreateOrderContract.Response> CreateAsync(CreateOrderContract.Request request)
        {
            var order = new Data.Entities.Order
            {
                Price = request.Price,
                Service = (OrderServiceEnum)request.Service,
                Status = OrderStatus.Pending,
                CreatedBy = string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();

            // Link the genetic inspection to this order
            var inspection = await dbContext.GeneticInspections.FindAsync(request.GeneticInspectionId);
            if (inspection is not null)
            {
                inspection.OrderId = order.Id;
                await dbContext.SaveChangesAsync();
            }

            return new CreateOrderContract.Response
            {
                Id = order.Id,
                Price = order.Price,
                Service = order.Service.ToString(),
                Status = order.Status.ToString(),
                GeneticInspectionId = request.GeneticInspectionId
            };
        }

        public async Task<GetOrderContract.Response?> GetByIdAsync(int id)
        {
            var order = await dbContext.Orders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null)
            {
                return null;
            }

            return new GetOrderContract.Response
            {
                Id = order.Id,
                Price = order.Price,
                Service = order.Service.ToString(),
                Status = order.Status.ToString(),
                GeneticInspectionId = order.GeneticInspection?.Id ?? 0,
                CreatedAt = order.CreatedAt,
                CreatedBy = order.CreatedBy,
                UpdatedAt = order.UpdatedAt,
                UpdatedBy = order.UpdatedBy
            };
        }

        public async Task<IEnumerable<GetOrderContract.Response>> GetAllAsync()
        {
            return await dbContext.Orders
                .AsNoTracking()
                .Include(o => o.GeneticInspection)
                .Select(order => new GetOrderContract.Response
                {
                    Id = order.Id,
                    Price = order.Price,
                    Service = order.Service.ToString(),
                    Status = order.Status.ToString(),
                    GeneticInspectionId = order.GeneticInspection != null ? order.GeneticInspection.Id : 0,
                    CreatedAt = order.CreatedAt,
                    CreatedBy = order.CreatedBy,
                    UpdatedAt = order.UpdatedAt,
                    UpdatedBy = order.UpdatedBy
                })
                .ToListAsync();
        }

        public async Task<GetOrderContract.Response?> UpdateAsync(int id, UpdateOrderContract.Request request)
        {
            var order = await dbContext.Orders
                .Include(o => o.GeneticInspection)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order is null)
            {
                return null;
            }

            order.Price = request.Price;
            order.Service = (OrderServiceEnum)request.Service;
            order.Status = request.Status;
            order.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            return new GetOrderContract.Response
            {
                Id = order.Id,
                Price = order.Price,
                Service = order.Service.ToString(),
                Status = order.Status.ToString(),
                GeneticInspectionId = order.GeneticInspection?.Id ?? 0,
                CreatedAt = order.CreatedAt,
                CreatedBy = order.CreatedBy,
                UpdatedAt = order.UpdatedAt,
                UpdatedBy = order.UpdatedBy
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var order = await dbContext.Orders.FindAsync(id);

            if (order is null)
            {
                return false;
            }

            dbContext.Orders.Remove(order);
            await dbContext.SaveChangesAsync();
            return true;
        }
    }
}
