using SmartOffice.Api.Enums;

namespace SmartOffice.Api.DTOs
{
    public class WorkspaceDto
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public int Capacity { get; set; }
        public Dictionary<Amenities, int> Amenities { get; set; } = new Dictionary<Amenities, int>();
    }
}
