using SmartOffice.Api.Enums;
using System;

namespace SmartOffice.Api.Entities
{
    public class Booking
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; }

        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public bool IsCheckInConfirmed { get; set; } = false;
        public BookingStatus Status { get; set; } = BookingStatus.Active;

        public Booking() { } 

        public Booking(Guid id, Guid userId, Guid workspaceId, DateTime startTime, DateTime endTime)
        {
            Id = id;
            UserId = userId;
            WorkspaceId = workspaceId;
            StartTime = startTime;
            EndTime = endTime;
        }
    }
}