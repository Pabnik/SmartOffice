using System;
using System.Collections.Generic;

namespace SmartOffice.Api.DTOs
{
    public class WorkspaceSearchResultDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public List<string> MatchedAmenities { get; set; } = new List<string>();
        public List<string> MissingAmenities { get; set; } = new List<string>();
        public int OccupancyPercentage { get; set; }
        public bool IsHighLoad { get; set; }
    }
}