using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using WebApplication1.Models.DTOs;
using WebApplication1.Repositories;
using System.Text.Json;

namespace WebApplication1.Services
{
    public class UserService
    {
        private readonly UserRepository _repository;
        private readonly IDistributedCache _cache;
        private readonly ILogger<UserService> _logger;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public UserService(UserRepository repository, IDistributedCache cache, ILogger<UserService> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            var cacheKey = "AllUsers";
            _logger.LogInformation("Checking Redis cache key: {CacheKey}", cacheKey);
            var cachedUsers = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedUsers))
            {
                _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
                return JsonSerializer.Deserialize<List<UserDto>>(cachedUsers)!;
            }

            _logger.LogInformation("Cache miss for key: {CacheKey}, querying database", cacheKey);
            var users = await _repository.GetAllUsersAsync();
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(users), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheDuration
            });
            _logger.LogInformation("Set Redis cache key: {CacheKey}", cacheKey);
            return users;
        }

        public async Task<UserDto> GetUserByIdAsync(string id)
        {
            var cacheKey = $"User:{id}";
            _logger.LogInformation("Checking Redis cache key: {CacheKey}", cacheKey);
            var cachedUser = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedUser))
            {
                _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
                return JsonSerializer.Deserialize<UserDto>(cachedUser)!;
            }

            _logger.LogInformation("Cache miss for key: {CacheKey}, querying database", cacheKey);
            var user = await _repository.GetUserByIdAsync(id);
            if (user != null)
            {
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(user), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheDuration
                });
                _logger.LogInformation("Set Redis cache key: {CacheKey}", cacheKey);
            }
            return user;
        }

        public async Task<IdentityUser> CreateUserAsync(CreateUserDto dto)
        {
            if (string.IsNullOrEmpty(dto.UserName) || string.IsNullOrEmpty(dto.Password))
                throw new ArgumentException("Username and password are required.");

            if (dto.UserName == "throwError")
                throw new InvalidOperationException("Simulated error for testing exception handling.");

            var user = await _repository.CreateUserAsync(dto);
            await _cache.RemoveAsync("AllUsers");
            _logger.LogInformation("Removed Redis cache key: AllUsers");
            return user;
        }

        public async Task<bool> UpdateUserAsync(string id, UserDto dto)
        {
            var success = await _repository.UpdateUserAsync(id, dto);
            if (success)
            {
                await _cache.RemoveAsync("AllUsers");
                await _cache.RemoveAsync($"User:{id}");
                _logger.LogInformation("Removed Redis cache keys: AllUsers, User:{Id}", id);
            }
            return success;
        }

        public async Task<bool> DeleteUserAsync(string id)
        {
            var success = await _repository.DeleteUserAsync(id);
            if (success)
            {
                await _cache.RemoveAsync("AllUsers");
                await _cache.RemoveAsync($"User:{id}");
                _logger.LogInformation("Removed Redis cache keys: AllUsers, User:{Id}", id);
            }
            return success;
        }

        public async Task<PagedResult<UserDto>> GetUsersAsync(
            string? search, string? sortBy, string? sortOrder, int pageNumber, int pageSize)
        {
            var cacheKey = $"PagedUsers:{search}:{sortBy}:{sortOrder}:{pageNumber}:{pageSize}";
            _logger.LogInformation("Checking Redis cache key: {CacheKey}", cacheKey);
            var cachedResult = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedResult))
            {
                _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
                return JsonSerializer.Deserialize<PagedResult<UserDto>>(cachedResult)!;
            }

            _logger.LogInformation("Cache miss for key: {CacheKey}, querying database", cacheKey);
            var result = await _repository.GetUsersAsync(search, sortBy, sortOrder, pageNumber, pageSize);
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheDuration
            });
            _logger.LogInformation("Set Redis cache key: {CacheKey}", cacheKey);
            return result;
        }
    }
}