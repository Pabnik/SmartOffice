using Microsoft.EntityFrameworkCore;
using SmartOffice.Api.Data;
using SmartOffice.Api.Entities;
using SmartOffice.Api.Services.Implementations;
using SmartOffice.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Ignore circular references in the database when generating JSON
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IBookingService, BookingService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHostedService<SmartOffice.Api.BackgroundTasks.AutoCancelService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var zombieBookings = dbContext.Bookings
        .Where(b => b.Status == SmartOffice.Api.Enums.BookingStatus.Active && b.EndTime < DateTime.UtcNow)
        .ToList();

    if (zombieBookings.Any())
    {
        foreach (var zombie in zombieBookings)
        {
            zombie.Status = SmartOffice.Api.Enums.BookingStatus.AutoCancelled;
        }
        dbContext.SaveChanges();
        Console.WriteLine($"[Cleanup] Fixed {zombieBookings.Count} zombie bookings on startup.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!dbContext.Workspaces.Any())
    {
        dbContext.Workspaces.Add(new MeetingRoom { Id = Guid.NewGuid(), Name = "Meeting Room A", Location = "104", Capacity = 4 });
        dbContext.Workspaces.Add(new MeetingRoom { Id = Guid.NewGuid(), Name = "Meeting Room B", Location = "104", Capacity = 8 });
        dbContext.Workspaces.Add(new MeetingRoom { Id = Guid.NewGuid(), Name = "Meeting Room C", Location = "104", Capacity = 15 });

        for (int i = 1; i <= 4; i++)
            dbContext.Workspaces.Add(new Desk { Id = Guid.NewGuid(), Name = $"Desk {i}", Location = "117", Capacity = 1 });

        for (int i = 1; i <= 8; i++)
            dbContext.Workspaces.Add(new Desk { Id = Guid.NewGuid(), Name = $"Desk {i}", Location = "123", Capacity = 1 });

        dbContext.Workspaces.Add(new MeetingRoom { Id = Guid.NewGuid(), Name = "Small Conference", Location = "212", Capacity = 6 });
        dbContext.Workspaces.Add(new MeetingRoom { Id = Guid.NewGuid(), Name = "Boardroom", Location = "212", Capacity = 12 });

        for (int i = 1; i <= 4; i++)
            dbContext.Workspaces.Add(new Desk { Id = Guid.NewGuid(), Name = $"Dev Desk {i}", Location = "241", Capacity = 1 });

        for (int i = 1; i <= 3; i++)
            dbContext.Workspaces.Add(new Desk { Id = Guid.NewGuid(), Name = $"Setup {i}", Location = "301", Capacity = 1 });

        dbContext.SaveChanges();
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();