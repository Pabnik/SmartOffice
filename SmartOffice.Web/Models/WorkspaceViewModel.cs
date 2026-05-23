using SmartOffice.Api.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartOffice.Web.Models
{
    public class WorkspaceViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        [Required]
        [Range(1, 50, ErrorMessage = "Capacity cannot exceed 50 people.")]
        public int Capacity { get; set; }

        public Dictionary<Amenities, int> Amenities { get; set; } = new Dictionary<Amenities, int>();

        public bool HasProjector { get; set; }
        public bool HasWhiteboard { get; set; }
        public bool HasMonitor { get; set; }
    }
}