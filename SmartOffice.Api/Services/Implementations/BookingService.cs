using Microsoft.EntityFrameworkCore;
using SmartOffice.Api.Data;
using SmartOffice.Api.Entities;
using SmartOffice.Api.Services.Interfaces;

namespace SmartOffice.Api.Services.Implementations
{
    public class BookingService : IBookingService
    {
        private readonly AppDbContext _dbContext;

        public BookingService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Booking>> GetUserBookingsAsync(Guid userId)
        {
            return await _dbContext.Bookings
                .Include(b => b.Workspace)
                .Where(b => b.UserId == userId
                            && b.Status != Enums.BookingStatus.CancelledByUser
                            && b.Status != Enums.BookingStatus.AutoCancelled
                            && b.EndTime > DateTime.UtcNow)
                .OrderBy(b => b.StartTime)
                .ToListAsync();
        }
        public async Task<List<Booking>> GetWorkspaceBookingsAsync(Guid workspaceId)
        {
            var now = DateTime.UtcNow;
            return await _dbContext.Bookings
                .Where(b => b.WorkspaceId == workspaceId && b.EndTime >= now)
                .OrderBy(b => b.StartTime)
                .ToListAsync();
        }

        public async Task CancelBookingAsync(Guid bookingId, Guid userId)
        {
            var booking = await _dbContext.Bookings.FindAsync(bookingId);

            if (booking == null)
                throw new Exception("Booking not found.");

            // Security check
            if (booking.UserId != userId)
                throw new Exception("Access denied. You can only cancel your own bookings.");

            booking.Status = Enums.BookingStatus.CancelledByUser;

            await _dbContext.SaveChangesAsync();
        }
    }
}
