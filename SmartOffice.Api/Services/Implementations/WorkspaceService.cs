using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartOffice.Api.Data;
using SmartOffice.Api.Entities;
using SmartOffice.Api.Services.Interfaces;

namespace SmartOffice.Api.Services.Implementations
{
    public class WorkspaceService : IWorkspaceService
    {
        private readonly AppDbContext _dbContext;

        public WorkspaceService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Workspace>> GetAllWorkspacesAsync()
        {
            return await _dbContext.Workspaces.ToListAsync();
        }

        public async Task<List<Workspace>> GetAvailableWorkspacesAsync(DateTime startTime, DateTime endTime)
        {
            // Validate time range
            if (startTime >= endTime)
                throw new ArgumentException("Start time must be before end time.");

            // 1. Find IDs of workspaces that are ALREADY booked during this period
            var bookedWorkspaceIds = await _dbContext.Bookings
                .Where(b => b.StartTime < endTime && b.EndTime > startTime)
                .Select(b => b.WorkspaceId)
                .ToListAsync();

            // 2. Select workspaces that are NOT in the booked list
            var availableWorkspaces = await _dbContext.Workspaces
                .Where(w => !bookedWorkspaceIds.Contains(w.Id))
                .ToListAsync();

            return availableWorkspaces;
        }

        public async Task<Booking> BookWorkspaceAsync(Guid workspaceId, Guid userId, DateTime start, DateTime end)
        {
            // 1. Basic time validation
            if (start >= end)
                throw new ArgumentException("Invalid booking time range.");

            // 2. Check if the workspace exists
            var workspace = await _dbContext.Workspaces.FindAsync(workspaceId);
            if (workspace == null)
                throw new Exception("Workspace not found.");

            // 3. Main logic: check for booking conflicts (overbooking)
            var isConflict = await _dbContext.Bookings
                .AnyAsync(b => b.WorkspaceId == workspaceId && b.StartTime < end && b.EndTime > start);

            if (isConflict)
                throw new Exception("This workspace is already booked for the selected time.");

            // 4. Create the booking
            var newBooking = new Booking(Guid.NewGuid(), userId, workspaceId, start, end);

            _dbContext.Bookings.Add(newBooking);
            await _dbContext.SaveChangesAsync();

            return newBooking;
        }
    }
}