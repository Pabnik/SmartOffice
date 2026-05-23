using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOffice.Api.Data;
using SmartOffice.Api.Entities;

namespace SmartOffice.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public UsersController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public class SyncUserDto
        {
            public long TelegramId { get; set; }
            public string Name { get; set; }
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncUser([FromBody] SyncUserDto request)
        {
            // Check if user already exists
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.TelegramId == request.TelegramId);

            if (user == null)
            {
                // Auto-register new user
                user = new User
                {
                    Id = Guid.NewGuid(),
                    TelegramId = request.TelegramId,
                    Name = request.Name ?? "TG User",
                    Email = $"{request.TelegramId}@telegram.bot" // Placeholder for DB
                };
                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();
            }

            return Ok(user);
        }
    }
}