using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOffice.Api.Data;
using SmartOffice.Api.DTOs;
using SmartOffice.Api.Services.Interfaces;
using System;
using System.Threading.Tasks;
using SmartOffice.TelegramBot;
using SmartOffice.Api.Entities;

namespace SmartOffice.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkspacesController : ControllerBase
    {
        private readonly IWorkspaceService _workspaceService;
        private readonly AppDbContext _dbContext;

        public WorkspacesController(IWorkspaceService workspaceService, AppDbContext dbContext)
        {
            _workspaceService = workspaceService;
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var workspaces = await _workspaceService.GetAllWorkspacesAsync();
            return Ok(workspaces);
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailable([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            try
            {
                var workspaces = await _workspaceService.GetAvailableWorkspacesAsync(start, end);
                return Ok(workspaces);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("book")]
        public async Task<IActionResult> Book([FromBody] BookWorkspaceRequest request)
        {
            try
            {
                var booking = await _workspaceService.BookWorkspaceAsync(
                    request.WorkspaceId,
                    request.UserId,
                    request.StartTime,
                    request.EndTime);

                return Ok(booking);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] int capacity, [FromQuery] List<string> amenities)
        {
            var workspaces = await _dbContext.Workspaces
                .Where(w => w.Capacity >= capacity)
                .ToListAsync();

            var overlappingBookings = await _dbContext.Bookings
                .Where(b => (b.Status == Enums.BookingStatus.Active || b.Status == Enums.BookingStatus.CheckedIn) &&
                            b.StartTime < end && b.EndTime > start)
                .Select(b => b.WorkspaceId)
                .Distinct()
                .ToListAsync();

            var availableWorkspaces = workspaces.Where(w => !overlappingBookings.Contains(w.Id)).ToList();

            var results = new List<DTOs.WorkspaceSearchResultDto>();

            var allWorkspacesInDb = await _dbContext.Workspaces.ToListAsync();

            foreach (var w in availableWorkspaces)
            {
                var matched = new List<string>();
                var missing = new List<string>();

                if (amenities != null && amenities.Any())
                {
                    foreach (var req in amenities)
                    {
                        if (Enum.TryParse<Enums.Amenities>(req, true, out var parsedAmenity) &&
                            w.Amenities != null &&
                            w.Amenities.ContainsKey(parsedAmenity) &&
                            w.Amenities[parsedAmenity] > 0)
                        {
                            matched.Add(req);
                        }
                        else
                        {
                            missing.Add(req);
                        }
                    }
                }

                int totalDesksInRoom = allWorkspacesInDb.Count(x => x.Location == w.Location);
                int bookedDesksInRoom = overlappingBookings.Count(id => allWorkspacesInDb.Any(ws => ws.Id == id && ws.Location == w.Location));

                int realLoad = totalDesksInRoom > 0 ? (int)Math.Round((double)bookedDesksInRoom / totalDesksInRoom * 100) : 0;

                results.Add(new DTOs.WorkspaceSearchResultDto
                {
                    Id = w.Id,
                    Name = w.Name,
                    Location = w.Location,
                    MatchedAmenities = matched,
                    MissingAmenities = missing,
                    OccupancyPercentage = realLoad,
                    IsHighLoad = realLoad >= 70
                });
            }

			results = results.OrderByDescending(r => r.MatchedAmenities.Count).Take(5).ToList();

			return Ok(results);
        }
        [HttpPost]
        public async Task<IActionResult> CreateWorkspace([FromBody] DTOs.WorkspaceDto request)
        {
            var workspace = new Entities.Desk
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Location = request.Location,
                Capacity = request.Capacity,
                Amenities = request.Amenities ?? new Dictionary<Enums.Amenities, int>()
            };

            _dbContext.Workspaces.Add(workspace);
            bool isDuplicate = await _dbContext.Workspaces
                .AnyAsync(w => w.Location == workspace.Location && w.Name == workspace.Name && w.Id != workspace.Id);

            if (isDuplicate)
            {
                return BadRequest("A workspace with this name already exists in this room.");
            }
            await _dbContext.SaveChangesAsync();

            return Ok(workspace);
        }
        [HttpGet("locations")]
        public async Task<IActionResult> GetUniqueLocations()
        {
            var locations = await _dbContext.Workspaces
                .Select(w => w.Location)
                .Distinct()
                .OrderBy(l => l)
                .ToListAsync();

            return Ok(locations);
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWorkspace(Guid id, [FromBody] DTOs.WorkspaceDto request)
        {
            var workspace = await _dbContext.Workspaces.FindAsync(id);
            if (workspace == null) return NotFound();

            workspace.Name = request.Name;
            workspace.Location = request.Location;
            workspace.Capacity = request.Capacity;
            workspace.Amenities = request.Amenities ?? new Dictionary<Enums.Amenities, int>();

            await _dbContext.SaveChangesAsync();
            return Ok(workspace);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWorkspace(Guid id)
        {
            var workspace = await _dbContext.Workspaces.FindAsync(id);
            if (workspace == null)
            {
                return NotFound();
            }

            var relatedBookings = await _dbContext.Bookings
                .Where(b => b.WorkspaceId == id)
                .ToListAsync();

            if (relatedBookings.Any())
            {
                _dbContext.Bookings.RemoveRange(relatedBookings);
            }

            _dbContext.Workspaces.Remove(workspace);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }
    }

}
