using System;
using System.Collections.Generic;
using SmartOffice.Api.Enums;

namespace SmartOffice.Api.Entities
{
    public class Workspace
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public int Capacity { get; set; }
        public Dictionary<Amenities, int> Amenities { get; set; } = new();

        public Workspace() { } 

        public Workspace(Guid id, string name, string location, int capacity, Dictionary<Amenities, int> amenities)
        {
            Id = id;
            Name = name;
            Location = location;
            Capacity = capacity;
            Amenities = amenities;
        }
    }

    public class Desk : Workspace
    {
        public Desk() { }
        public Desk(Guid id, string name, string location, int capacity, Dictionary<Amenities, int> amenities)
            : base(id, name, location, capacity, amenities) { }
    }

    public class MeetingRoom : Workspace
    {
        public MeetingRoom() { }
        public MeetingRoom(Guid id, string name, string location, int capacity, Dictionary<Amenities, int> amenities)
            : base(id, name, location, capacity, amenities) { }
    }
}