namespace SmartOffice.Api.DTOs
{
    public class BookWorkspaceRequest
    {
        public Guid WorkspaceId { get; set; }
        public Guid UserId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}
