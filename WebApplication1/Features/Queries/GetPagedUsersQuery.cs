using MediatR;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Features.Queries
{
    public class GetPagedUsersQuery : IRequest<PagedResult<UserDto>>
    {
        public string? Search { get; set; }
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        public GetPagedUsersQuery(string? search, string? sortBy, string? sortOrder, int pageNumber, int pageSize)
        {
            Search = search;
            SortBy = sortBy;
            SortOrder = sortOrder;
            PageNumber = pageNumber;
            PageSize = pageSize;
        }
    }
}