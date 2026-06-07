using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOffice.Api.Services.Interfaces;
using System;
using System.Threading.Tasks;
using SmartOffice.Api.Data;

namespace SmartOffice.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingsController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly AppDbContext _dbContext;

        public BookingsController(IBookingService bookingService, AppDbContext dbContext)
        {
            _bookingService = bookingService;
            _dbContext = dbContext;
        }

        [HttpGet("my/{userId}")]
        public async Task<IActionResult> GetMyBookings(Guid userId)
        {
            var bookings = await _bookingService.GetUserBookingsAsync(userId);

            var safeDtos = bookings.Select(b => new
            {
                Id = b.Id,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                IsCheckInConfirmed = b.IsCheckInConfirmed,
                Workspace = b.Workspace == null ? null : new
                {
                    Id = b.Workspace.Id,
                    Name = b.Workspace.Name,
                    Location = b.Workspace.Location
                }
            });

            return Ok(safeDtos);
        }

        [HttpGet("workspace/{workspaceId}")]
        public async Task<IActionResult> GetWorkspaceBookings(Guid workspaceId)
        {
            var bookings = await _bookingService.GetWorkspaceBookingsAsync(workspaceId);
            return Ok(bookings);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBooking(Guid id)
        {
            var booking = await _dbContext.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            booking.Status = Enums.BookingStatus.CancelledByUser;

            await _dbContext.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("{id}/checkin")]
        public async Task<IActionResult> CheckIn(Guid id, [FromQuery] Guid userId)
        {   
            var booking = await _dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (booking == null)
                return NotFound("Booking not found.");

            if (booking.IsCheckInConfirmed)
                return BadRequest("Already checked in.");

            booking.IsCheckInConfirmed = true;
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Check-in successful" });
        }
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics([FromQuery] string period = "all")
        {
            var availableMonths = await _dbContext.Bookings
                .Select(b => new { b.StartTime.Year, b.StartTime.Month })
                .Distinct()
                .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
                .ToListAsync();

            var availablePeriods = availableMonths
                .Select(m => $"{m.Year}-{m.Month:D2}")
                .ToList();

            var query = _dbContext.Bookings.AsQueryable();

            DateTime trendEndDate = DateTime.Today;

            if (period != "all" && !string.IsNullOrEmpty(period))
            {
                var parts = period.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month))
                {
                    query = query.Where(b => b.StartTime.Year == year && b.StartTime.Month == month);

                    if (year != DateTime.Today.Year || month != DateTime.Today.Month)
                    {
                        trendEndDate = new DateTime(year, month, DateTime.DaysInMonth(year, month));
                    }
                }
            }

            var all = await query.ToListAsync();
            var workspacesDictionary = await _dbContext.Workspaces.ToDictionaryAsync(w => w.Id, w => w.Name + " (Room " + w.Location + ")");

            double totalHoursBooked = all.Sum(b => (b.EndTime - b.StartTime).TotalHours);
            double hoursActuallyUsed = all.Where(b => b.Status == Enums.BookingStatus.CheckedIn)
                                          .Sum(b => (b.EndTime - b.StartTime).TotalHours);

            double hoursSavedBySystem = all.Where(b => b.Status == Enums.BookingStatus.AutoCancelled)
                                           .Sum(b => (b.EndTime - b.StartTime).TotalHours - 0.25);

            var trend = new List<object>();
            for (int i = 6; i >= 0; i--)
            {
                var date = trendEndDate.AddDays(-i);
                var dailyBookings = all.Where(b => b.StartTime.Date == date.Date).ToList();

                double dailyTotal = dailyBookings.Sum(b => (b.EndTime - b.StartTime).TotalHours);
                double dailyActual = dailyBookings
                    .Where(b => b.Status == Enums.BookingStatus.CheckedIn)
                    .Sum(b => (b.EndTime - b.StartTime).TotalHours);

                double dailyUtil = dailyTotal > 0 ? Math.Round((dailyActual / dailyTotal) * 100, 1) : 0;

                trend.Add(new { Date = date.ToString("dd MMM"), Rate = dailyUtil });
            }

            var topWorkspaces = all.GroupBy(b => b.WorkspaceId)
                                   .Select(g => new {
                                       Name = workspacesDictionary.ContainsKey(g.Key) ? workspacesDictionary[g.Key] : "Deleted Desk",
                                       Count = g.Count()
                                   })
                                   .OrderByDescending(x => x.Count)
                                   .Take(5)
                                   .ToList();

            var peakHours = new List<object>();
            for (int h = 8; h <= 20; h++)
            {
				int count = all.Count(b => b.StartTime.Hour <= h && b.EndTime.Hour > h);
				peakHours.Add(new { Hour = $"{h:00}:00", Count = count });
			}

            double energySaved = hoursSavedBySystem * 0.3;
            double noShowRate = all.Count > 0
                ? Math.Round((double)all.Count(b => b.Status == Enums.BookingStatus.AutoCancelled) / all.Count * 100, 1)
                : 0;

            var autoCancelled = all.Where(b => b.Status == Enums.BookingStatus.AutoCancelled).ToList();

            IEnumerable<object> savedHoursTrend;

            if (period == "all" || string.IsNullOrEmpty(period))
            {
                savedHoursTrend = autoCancelled
                    .GroupBy(b => new { b.StartTime.Year, b.StartTime.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new
                    {
                        Label = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        Hours = Math.Round(g.Sum(b => (b.EndTime - b.StartTime).TotalHours), 1)
                    });
            }
            else
            {
                savedHoursTrend = autoCancelled
                    .GroupBy(b => b.StartTime.ToLocalTime().Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        Label = g.Key.ToString("dd MMM"),
                        Hours = Math.Round(g.Sum(b => (b.EndTime - b.StartTime).TotalHours), 1)
                    });
            }
			var stats = new
			{
				TotalCount = all.Count,
				UtilizationRate = totalHoursBooked > 0 ? Math.Round((hoursActuallyUsed / totalHoursBooked) * 100, 1) : 0,
				HoursSaved = Math.Round(hoursSavedBySystem, 1),
				AutoCancelledCount = autoCancelled.Count,
				CheckedInCount = all.Count(b => b.Status == Enums.BookingStatus.CheckedIn),
				ActiveCount = all.Count(b => b.Status == Enums.BookingStatus.Active),

				CancelledByUserCount = all.Count(b => b.Status == Enums.BookingStatus.CancelledByUser),

				DailyTrend = trend,
				TopWorkspaces = topWorkspaces,
				PeakHours = peakHours,

				TotalHoursBooked = Math.Round(totalHoursBooked, 1),
				ActualHoursUsed = Math.Round(hoursActuallyUsed, 1),
				EnergySavedKwh = Math.Round(energySaved, 1),
				NoShowRate = noShowRate,
				AvailablePeriods = availablePeriods,

				SavedHoursTrend = savedHoursTrend.ToList()
			};

			return Ok(stats);
		}

		[HttpPost("seed-simulation/{mode}")]
		public async Task<IActionResult> SeedSimulationData(string mode)
		{
			mode = mode.ToLower();
			if (mode != "before" && mode != "after")
				return BadRequest("Parameter must be 'before' or 'after'.");

			_dbContext.Bookings.RemoveRange(_dbContext.Bookings);
			await _dbContext.SaveChangesAsync();

			var random = new Random();
			var workspaces = await _dbContext.Workspaces.ToListAsync();

			if (!workspaces.Any())
				return BadRequest("Need workspaces to simulate.");

			var ghostUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Name == "SimulationBot");
			if (ghostUser == null)
			{
				ghostUser = new Entities.User
				{
					Id = Guid.NewGuid(),
					TelegramId = 11111111,
					Name = "SimulationBot",
					Email = "bot@smartoffice.com"
				};
				_dbContext.Users.Add(ghostUser);
				await _dbContext.SaveChangesAsync();
			}

			var simulationBookings = new List<Entities.Booking>();
			var realNowLocal = DateTime.Now;

			var fakeNowUtc = DateTime.SpecifyKind(realNowLocal, DateTimeKind.Utc);

			for (int day = -60; day <= 7; day++)
			{
				var dateLocal = realNowLocal.AddDays(day).Date;

				if (dateLocal.DayOfWeek == DayOfWeek.Saturday || dateLocal.DayOfWeek == DayOfWeek.Sunday) continue;

				int bookingsCount = (mode == "after") ? random.Next(30, 50) : random.Next(15, 25);

				for (int i = 0; i < bookingsCount; i++)
				{
					var workspace = workspaces[random.Next(workspaces.Count)];

					int startHour = random.Next(8, 20);
					int startMinute = random.Next(0, 4) * 15;
					var startLocal = dateLocal.AddHours(startHour).AddMinutes(startMinute);

					int durationHours = random.Next(1, 4);
					var endLocal = startLocal.AddHours(durationHours);

					if (endLocal.Hour > 20 || (endLocal.Hour == 20 && endLocal.Minute > 0))
					{
						endLocal = dateLocal.AddHours(20);
					}

					var booking = new Entities.Booking
					{
						Id = Guid.NewGuid(),
						WorkspaceId = workspace.Id,
						UserId = ghostUser.Id,

						StartTime = DateTime.SpecifyKind(startLocal, DateTimeKind.Utc),
						EndTime = DateTime.SpecifyKind(endLocal, DateTimeKind.Utc)
					};

					int roll = random.Next(100);

					if (booking.EndTime <= fakeNowUtc)
					{
						if (mode == "after")
						{
							if (roll < 65) { booking.Status = Enums.BookingStatus.CheckedIn; booking.IsCheckInConfirmed = true; }
							else if (roll < 75) { booking.Status = Enums.BookingStatus.CancelledByUser; booking.IsCheckInConfirmed = false; }
							else { booking.Status = Enums.BookingStatus.AutoCancelled; booking.IsCheckInConfirmed = false; }
						}
						else
						{
							if (roll < 50) { booking.Status = Enums.BookingStatus.CheckedIn; booking.IsCheckInConfirmed = true; }
							else { booking.Status = Enums.BookingStatus.CancelledByUser; booking.IsCheckInConfirmed = false; }
						}
					}
					else
					{
						booking.Status = Enums.BookingStatus.Active;
						booking.IsCheckInConfirmed = false;
					}

					bool isOverlap = simulationBookings.Any(b =>
						b.WorkspaceId == workspace.Id &&
						b.Status != Enums.BookingStatus.CancelledByUser &&
						b.Status != Enums.BookingStatus.AutoCancelled &&
						b.StartTime < booking.EndTime && b.EndTime > booking.StartTime);

					if (!isOverlap)
					{
						simulationBookings.Add(booking);
					}
				}
			}

			_dbContext.Bookings.AddRange(simulationBookings);
			await _dbContext.SaveChangesAsync();

			return Ok(new { Message = $"Сценарій '{mode.ToUpper()}' згенеровано! Записів: {simulationBookings.Count}. Графік відкалібровано до 20:00." });
		}
	}
}