using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartOffice.Api.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace SmartOffice.Api.BackgroundTasks
{
    public class AutoCancelService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoCancelService> _logger;

        public AutoCancelService(IServiceProvider serviceProvider, ILogger<AutoCancelService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoCancelService background task is starting.");

            // Run an infinite loop while the application is alive
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessAbandonedBookingsAsync();

                // Pause for 1 minute before checking again
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessAbandonedBookingsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;
            var thresholdTime = now.AddMinutes(-15);

            // Include User and Workspace to get TelegramId and Room details
            var abandonedBookings = await dbContext.Bookings
                .Include(b => b.User)
                .Include(b => b.Workspace)
                .Where(b => b.Status == Enums.BookingStatus.Active
                         && b.IsCheckInConfirmed == false
                         && b.StartTime <= thresholdTime
                         && b.EndTime > now)
                .ToListAsync();

            if (abandonedBookings.Any())
            {
                _logger.LogWarning($"Found {abandonedBookings.Count} abandoned bookings. Changing status to AutoCancelled...");

                // Initialize Bot Client
                var botToken = "8600635947:AAHdU0HYgtHsG45XoVs88Vj9X9mNE4ngdcM";
                var botClient = new Telegram.Bot.TelegramBotClient(botToken);

                foreach (var booking in abandonedBookings)
                {
                    booking.Status = Enums.BookingStatus.AutoCancelled;

                    // Send notification to the user
                    try
                    {
                        string msg = $"⚠️ **Auto-Cancellation Alert**\n\n" +
                                     $"Your booking for **{booking.Workspace.Name}** (Room {booking.Workspace.Location}) " +
                                     $"has been automatically cancelled because Check-In was not confirmed within 15 minutes of the start time.\n\n" +
                                     $"The workspace is now available for other colleagues.";

                        await botClient.SendMessage(booking.User.TelegramId, msg, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to send Telegram notification to user {booking.UserId}: {ex.Message}");
                    }
                }

                await dbContext.SaveChangesAsync();
            }
        }
    }
}