using SmartOffice.Api.Entities;

namespace SmartOffice.Api.Services.Interfaces
{
    public interface IWorkspaceService
    {
        Task<List<Workspace>> GetAllWorkspacesAsync();
        Task<List<Workspace>> GetAvailableWorkspacesAsync(DateTime startTime, DateTime endTime);
        Task<Booking> BookWorkspaceAsync(Guid workspaceId, Guid userId, DateTime start, DateTime end);
    }
}
