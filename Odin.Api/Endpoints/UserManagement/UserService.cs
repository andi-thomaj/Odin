using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.Endpoints.UserManagement
{
    public interface IUserService
    {
        Task<CreateUserContract.Response> CreateUserAsync(CreateUserContract.Request request);
    }

    public class UserService : IUserService
    {
        public async Task<CreateUserContract.Response> CreateUserAsync(CreateUserContract.Request request)
        {
            // Simulate user creation
            await Task.Delay(1000);

            return new CreateUserContract.Response
            {
                FirstName = "Andi",
                LastName = "Thomaj",
                Email = "andi.dev94@gmail.com"
            };
        }
    }
}
