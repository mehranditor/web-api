using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Swagger
{
    public class UserDtoExample : IExamplesProvider<UserDto>
    {
        public UserDto GetExamples()
        {
            return new UserDto
            {
                Id = "7f5d2747-9a1e-40f9-af33-cfceab1bdf06",
                UserName = "MehranUpdated",
                Email = "mehran.updated@gmail.com"
            };
        }
    }

    public class UsersListExample : IExamplesProvider<List<UserDto>>
    {
        public List<UserDto> GetExamples()
        {
            return new List<UserDto>
            {
                new UserDto { Id = "7f5d2747-9a1e-40f9-af33-cfceab1bdf06", UserName = "Mehran", Email = "mehran@gmail.com" },
                new UserDto { Id = "8f5d2747-9a1e-40f9-af33-cfceab1bdf07", UserName = "Ali", Email = "ali@gmail.com" }
            };
        }
    }

    public class ProblemDetailsExample : IExamplesProvider<Microsoft.AspNetCore.Mvc.ProblemDetails>
    {
        public Microsoft.AspNetCore.Mvc.ProblemDetails GetExamples()
        {
            return new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Title = "Not Found",
                Status = 404,
                Detail = "The user with the specified ID was not found.",
                Instance = "/api/User/7f5d2747-9a1e-40f9-af33-cfceab1bdf06"
            };
        }
    }
}