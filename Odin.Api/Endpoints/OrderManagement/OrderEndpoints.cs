using Microsoft.AspNetCore.Mvc;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.OrderManagement
{
    public static class OrderEndpoints
    {
        public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/orders");

            endpoints.MapGet("/", GetAll).RequireAuthorization("Authenticated");
            endpoints.MapGet("/{id:int}", GetById).RequireAuthorization("Authenticated");
            endpoints.MapPost("/", Create).RequireAuthorization("ScientistOrAdmin");
            endpoints.MapPut("/{id:int}", Update).RequireAuthorization("ScientistOrAdmin");
            endpoints.MapDelete("/{id:int}", Delete).RequireAuthorization("AdminOnly");
        }

        private static async Task<IResult> GetAll(IOrderService service)
        {
            var orders = await service.GetAllAsync();
            return Results.Ok(orders);
        }

        private static async Task<IResult> GetById(IOrderService service, int id)
        {
            var order = await service.GetByIdAsync(id);

            return order is null
                ? Results.NotFound(new { Message = $"Order with ID {id} not found." })
                : Results.Ok(order);
        }

        private static async Task<IResult> Create(
            IOrderService service,
            [FromBody] CreateOrderContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var response = await service.CreateAsync(request);
            return Results.Created($"/api/orders/{response.Id}", response);
        }

        private static async Task<IResult> Update(
            IOrderService service,
            int id,
            [FromBody] UpdateOrderContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var response = await service.UpdateAsync(id, request);

            return response is null
                ? Results.NotFound(new { Message = $"Order with ID {id} not found." })
                : Results.Ok(response);
        }

        private static async Task<IResult> Delete(IOrderService service, int id)
        {
            var deleted = await service.DeleteAsync(id);

            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { Message = $"Order with ID {id} not found." });
        }
    }
}
