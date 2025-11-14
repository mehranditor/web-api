using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Repositories
{
    public class UserRepository
    {
        private readonly UserManager<IdentityUser> _userManager;

        public UserRepository(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            return await _userManager.Users
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email
                })
                .ToListAsync();
        }

        public async Task<UserDto?> GetUserByIdAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return null;

            return new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email
            };
        }

        public async Task<IdentityUser> CreateUserAsync(CreateUserDto dto)
        {
            var user = new IdentityUser
            {
                UserName = dto.UserName,
                Email = dto.Email
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));

            return user;
        }

        public async Task<bool> UpdateUserAsync(string id, UserDto dto)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return false;

            user.UserName = dto.UserName;
            user.Email = dto.Email;

            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded;
        }

        public async Task<bool> DeleteUserAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return false;

            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded;
        }

        // ✅ Pagination, Filtering, Sorting
        public async Task<PagedResult<UserDto>> GetUsersAsync(
            string? search, string? sortBy, string? sortOrder, int pageNumber, int pageSize)
        {
            var query = _userManager.Users.AsQueryable();

            // 🔍 Filtering
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u =>
                    u.UserName.Contains(search) || u.Email.Contains(search));
            }

            // ↕ Sorting
            query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
            {
                ("username", "desc") => query.OrderByDescending(u => u.UserName),
                ("username", _) => query.OrderBy(u => u.UserName),
                ("email", "desc") => query.OrderByDescending(u => u.Email),
                ("email", _) => query.OrderBy(u => u.Email),
                _ => query.OrderBy(u => u.UserName) // default
            };

            // 📊 Pagination
            var totalCount = await query.CountAsync();
            var users = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<UserDto>
            {
                Items = users.Select(u => new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email
                }).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
    }
}
