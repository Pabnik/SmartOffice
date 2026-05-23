using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SmartOffice.Api.Entities;
using SmartOffice.Api.Enums;
using System.Text.Json;

namespace SmartOffice.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Workspace> Workspaces { get; set; }
        public DbSet<Booking> Bookings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Workspace>()
                .HasDiscriminator<string>("WorkspaceType")
                .HasValue<Desk>("Desk")
                .HasValue<MeetingRoom>("MeetingRoom");

            var dictionaryComparer = new ValueComparer<Dictionary<Amenities, int>>(
                (c1, c2) => c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                c => c.ToDictionary(kv => kv.Key, kv => kv.Value));

            modelBuilder.Entity<Workspace>()
                .Property(w => w.Amenities)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<Amenities, int>>(v, (JsonSerializerOptions?)null)
                )
                .Metadata.SetValueComparer(dictionaryComparer);
        }
    }
}
