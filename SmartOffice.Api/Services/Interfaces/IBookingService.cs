using SmartOffice.Api.Entities;

namespace SmartOffice.Api.Services.Interfaces
{
    public interface IBookingService
    {
        Task<List<Booking>> GetWorkspaceBookingsAsync(Guid workspaceId);
        Task<List<Booking>> GetUserBookingsAsync(Guid userId);
        Task CancelBookingAsync(Guid bookingId, Guid userId);
    }
}
