using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SmartOffice.TelegramBot
{
    class Program
    {
        private static readonly string BotToken = "8600635947:AAHdU0HYgtHsG45XoVs88Vj9X9mNE4ngdcM";
        private static readonly HttpClient ApiClient = new HttpClient { BaseAddress = new Uri("https://localhost:7227/") };
        private static readonly Dictionary<long, Guid> UserCache = new();

        private static readonly Dictionary<long, BookingState> ActiveBookings = new();
        private static readonly Dictionary<long, SmartSearchState> ActiveSearches = new();

        static async Task Main(string[] args)
        {
            var botClient = new TelegramBotClient(BotToken);
            using var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            var me = await botClient.GetMe();
            Console.WriteLine($"Bot @{me.Username} is running...");
            Console.WriteLine("Press Enter to stop the bot.");
            Console.ReadLine();

            cts.Cancel();
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                await ProcessCallbackQuery(botClient, update.CallbackQuery, cancellationToken);
                return;
            }

            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                await ProcessTextMessage(botClient, update.Message, cancellationToken);
            }
        }

        private static InlineKeyboardMarkup GetAmenitiesKeyboard(SmartSearchState state)
        {
            string projMark = state.SelectedAmenities.Contains("Projector") ? "✅ " : "⬜️ ";
            string boardMark = state.SelectedAmenities.Contains("Whiteboard") ? "✅ " : "⬜️ ";
            string monMark = state.SelectedAmenities.Contains("Monitor") ? "✅ " : "⬜️ ";

            return new InlineKeyboardMarkup(new[]
            {
                new [] { InlineKeyboardButton.WithCallbackData($"{projMark}Projector", "amenity_Projector") },
                new [] { InlineKeyboardButton.WithCallbackData($"{boardMark}Whiteboard", "amenity_Whiteboard") },
                new [] { InlineKeyboardButton.WithCallbackData($"{monMark}Monitors", "amenity_Monitor") },
                new [] { InlineKeyboardButton.WithCallbackData("➡️ Continue", "amenity_continue") }
            });
        }

        private static async Task ProcessCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message!.Chat.Id;
            var messageId = callbackQuery.Message.MessageId;

            if (callbackQuery.Data!.StartsWith("cancel_"))
            {
                var bookingId = callbackQuery.Data.Replace("cancel_", "");
                var dbUserId = await GetOrCreateUserIdAsync(chatId, "User");

                try
                {
                    var response = await ApiClient.DeleteAsync($"api/bookings/{bookingId}", cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        await botClient.EditMessageText(chatId, messageId, "✅ Booking successfully cancelled.", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.EditMessageText(chatId, messageId, "⚠️ Failed to cancel the booking.", cancellationToken: cancellationToken);
                    }
                }
                catch
                {
                    await botClient.EditMessageText(chatId, messageId, "❌ API error while cancelling.", cancellationToken: cancellationToken);
                }
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }

            if (callbackQuery.Data == "stop_search")
            {
                ActiveSearches.Remove(chatId);
                await botClient.EditMessageText(chatId, messageId, "🔍 Search cancelled.", cancellationToken: cancellationToken);
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }

            if (callbackQuery.Data!.StartsWith("amenity_"))
            {
                if (ActiveSearches.TryGetValue(chatId, out var state) && state.Step == 2)
                {
                    var action = callbackQuery.Data.Replace("amenity_", "");

                    if (action == "continue")
                    {
                        state.Step = 3;
                        string exampleDate = DateTime.Now.AddDays(1).ToString("dd.MM.yyyy");
                        string chosen = state.SelectedAmenities.Any() ? string.Join(", ", state.SelectedAmenities) : "None";

                        await botClient.EditMessageText(chatId, messageId,
                            $"✅ Space for {state.Capacity} person(s).\n" +
                            $"✅ Selected: {chosen}\n\n" +
                            $"Now, enter the date and time in this exact format:\n`DD.MM.YYYY HH:mm-HH:mm`\n\nExample: `{exampleDate} 10:00-14:00`",
                            parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        if (state.SelectedAmenities.Contains(action)) state.SelectedAmenities.Remove(action);
                        else state.SelectedAmenities.Add(action);

                        await botClient.EditMessageReplyMarkup(chatId, messageId, replyMarkup: GetAmenitiesKeyboard(state), cancellationToken: cancellationToken);
                    }
                }
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }
            // --- Handle Check-In ---
            if (callbackQuery.Data!.StartsWith("checkin_"))
            {
                var bookingId = callbackQuery.Data.Replace("checkin_", "");
                var dbUserId = await GetOrCreateUserIdAsync(chatId, "User");

                try
                {
                    var content = new StringContent(string.Empty);
                    var response = await ApiClient.PostAsync($"api/bookings/{bookingId}/checkin?userId={dbUserId}", content, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        await botClient.EditMessageText(chatId, messageId, "✅ **Check-in successful!** Have a productive day.", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.AnswerCallbackQuery(callbackQuery.Id, "⚠️ Check-in failed or already confirmed.", showAlert: true, cancellationToken: cancellationToken);
                    }
                }
                catch (Exception)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Server error.", showAlert: true, cancellationToken: cancellationToken);
                }

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }

            // --- Handle Smart Booking Quick Book ---
            if (callbackQuery.Data!.StartsWith("smartbook_"))
            {
                // Verify if the bot remembers the session and is on the final step (4)
                if (!ActiveSearches.TryGetValue(chatId, out var searchState) || searchState.Step != 4)
                {
                    await botClient.EditMessageText(chatId, messageId, "⚠️ Search session expired. Please start a new search.", cancellationToken: cancellationToken);
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                    return;
                }

                var workspaceId = Guid.Parse(callbackQuery.Data.Replace("smartbook_", ""));
                var dbUserId = await GetOrCreateUserIdAsync(chatId, "User");

                var bookRequest = new BookRequest
                {
                    WorkspaceId = workspaceId,
                    UserId = dbUserId,
                    StartTime = searchState.StartTime,
                    EndTime = searchState.EndTime
                };

                try
                {
                    var response = await ApiClient.PostAsJsonAsync("api/workspaces/book", bookRequest, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        ActiveSearches.Remove(chatId);
                        await botClient.EditMessageText(chatId, messageId,
                            $"🎉 **Booking Confirmed!**\n\n📅 Date: {searchState.StartTime.ToLocalTime():dd.MM.yyyy}\n⏰ Time: {searchState.StartTime.ToLocalTime():HH:mm} - {searchState.EndTime.ToLocalTime():HH:mm}",
                            parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.EditMessageText(chatId, messageId, "⚠️ Someone just booked this workspace!", cancellationToken: cancellationToken);
                    }
                }
                catch
                {
                    await botClient.EditMessageText(chatId, messageId, "❌ Server connection error.", cancellationToken: cancellationToken);
                }

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }
            // --- Handle Back to Rooms ---
            if (callbackQuery.Data == "back_to_rooms")
            {
                try
                {
                    // Fetch unique locations dynamically from the API
                    var locations = await ApiClient.GetFromJsonAsync<List<string>>("api/workspaces/locations", cancellationToken);
                    var buttons = new List<List<InlineKeyboardButton>>();

                    if (locations != null)
                    {
                        for (int i = 0; i < locations.Count; i += 2)
                        {
                            var row = new List<InlineKeyboardButton>();

                            // First button in the row
                            row.Add(InlineKeyboardButton.WithCallbackData($"Room {locations[i]}", $"room_{locations[i]}"));

                            // Second button in the row (if exists)
                            if (i + 1 < locations.Count)
                            {
                                row.Add(InlineKeyboardButton.WithCallbackData($"Room {locations[i + 1]}", $"room_{locations[i + 1]}"));
                            }

                            buttons.Add(row);
                        }
                    }

                    var roomKeyboard = new InlineKeyboardMarkup(buttons);

                    await botClient.EditMessageText(
                        chatId: chatId,
                        messageId: messageId,
                        text: "Please select a room to see available workspaces:",
                        replyMarkup: roomKeyboard,
                        cancellationToken: cancellationToken);
                }
                catch (Exception)
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Error loading rooms. Please try again later.", showAlert: true, cancellationToken: cancellationToken);
                }

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }

            // --- Standard Booking Menu ---//
            // --- 1. Dynamic Room Menu (Shows desks inside a selected room) ---
            if (callbackQuery.Data!.StartsWith("room_"))
            {
                string roomName = callbackQuery.Data.Replace("room_", "");

                // Fetch all workspaces and filter by the selected room
                var workspaces = await ApiClient.GetFromJsonAsync<WorkspaceDto[]>("api/workspaces", cancellationToken);
                var roomDesks = workspaces?.Where(w => w.Location == roomName).OrderBy(w => w.Name).ToList();

                if (roomDesks == null || !roomDesks.Any())
                {
                    await botClient.AnswerCallbackQuery(callbackQuery.Id, "No workspaces available in this room yet.", showAlert: true, cancellationToken: cancellationToken);
                    return;
                }

                string text = $"🚪 Room {roomName}:\nPlease select a workspace:";
                var buttons = new List<List<InlineKeyboardButton>>();

                // Generate buttons dynamically. 
                foreach (var desk in roomDesks)
                {
                    string icons = "";

                    if (desk.Amenities != null)
                    {
                        if (desk.Amenities.ContainsKey(Amenities.Monitor)) icons += "🖥️";
                        if (desk.Amenities.ContainsKey(Amenities.Whiteboard)) icons += "📋";
                        if (desk.Amenities.ContainsKey(Amenities.Projector)) icons += "📽️";
                    }

                    // Fallback icon if a desk has absolutely no amenities checked
                    if (string.IsNullOrEmpty(icons))
                    {
                        icons = "🏢";
                    }

                    buttons.Add(new List<InlineKeyboardButton>
                    {
                        InlineKeyboardButton.WithCallbackData($"{icons} {desk.Name}", $"desk_{desk.Id}")
                    });
                }

                // Add back button
                buttons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData("🔙 Back to Rooms", "back_to_rooms") });

                var deskKeyboard = new InlineKeyboardMarkup(buttons);

                await botClient.EditMessageText(chatId, messageId, text, replyMarkup: deskKeyboard, cancellationToken: cancellationToken);
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }
            if (callbackQuery.Data!.StartsWith("room_"))
            {
                string roomName = callbackQuery.Data.Replace("room_", "");
                InlineKeyboardMarkup deskKeyboard = null;
                string text = "";
                var backButton = new[] { InlineKeyboardButton.WithCallbackData("🔙 Back to Rooms", "back_to_rooms") };

                switch (roomName)
                {
                    case "104":
                        text = "🚪 Room 104 (Meeting Rooms):\nChoose a suitable room for your team:";
                        deskKeyboard = new InlineKeyboardMarkup(new[] {
                            new [] { InlineKeyboardButton.WithCallbackData("Meeting Room A (4 pax) 👥", "desk_104_A") },
                            new [] { InlineKeyboardButton.WithCallbackData("Meeting Room B (8 pax) 👥", "desk_104_B") },
                            new [] { InlineKeyboardButton.WithCallbackData("Meeting Room C (15 pax) 👥📽️", "desk_104_C") },
                            backButton
                        });
                        break;
                    case "117":
                        text = "🚪 Room 117 (Project Room):\n4 places available. Legend:\n🖥️ Monitor | 📋 Whiteboard | 📽️ Projector";
                        deskKeyboard = new InlineKeyboardMarkup(new[] {
                            new [] { InlineKeyboardButton.WithCallbackData("Desk 1 🖥️📋", "desk_117_1"), InlineKeyboardButton.WithCallbackData("Desk 2 🖥️📋", "desk_117_2") },
                            new [] { InlineKeyboardButton.WithCallbackData("Desk 3 🖥️", "desk_117_3"), InlineKeyboardButton.WithCallbackData("Desk 4 🖥️📽️", "desk_117_4") },
                            backButton
                        });
                        break;
                    case "123":
                        text = "🚪 Room 123 (Open Space):\n8 places available. All equipped with monitors.";
                        deskKeyboard = new InlineKeyboardMarkup(new[] {
                            new [] { InlineKeyboardButton.WithCallbackData("Desk 1 🖥️", "desk_123_1"), InlineKeyboardButton.WithCallbackData("Desk 2 🖥️", "desk_123_2") },
                            new [] { InlineKeyboardButton.WithCallbackData("Desk 3 🖥️", "desk_123_3"), InlineKeyboardButton.WithCallbackData("Desk 4 🖥️", "desk_123_4") },
                            new [] { InlineKeyboardButton.WithCallbackData("Desk 5 🖥️", "desk_123_5"), InlineKeyboardButton.WithCallbackData("Desk 6 🖥️📋", "desk_123_6") },
                            new [] { InlineKeyboardButton.WithCallbackData("Desk 7 🖥️📋", "desk_123_7"), InlineKeyboardButton.WithCallbackData("Desk 8 🖥️📋", "desk_123_8") },
                            backButton
                        });
                        break;
                    case "212":
                        text = "🚪 Room 212 (2nd Floor Meeting Rooms):\nChoose a suitable room:";
                        deskKeyboard = new InlineKeyboardMarkup(new[] {
                            new [] { InlineKeyboardButton.WithCallbackData("Small Conference (6 pax) 👥", "desk_212_1") },
                            new [] { InlineKeyboardButton.WithCallbackData("Boardroom (12 pax) 👥📋", "desk_212_2") },
                            backButton
                        });
                        break;
                    case "241":
                        text = "🚪 Room 241 (Developer Hub):\nQuiet zone for focused coding. Dual monitors included.";
                        deskKeyboard = new InlineKeyboardMarkup(new[] {
                            new [] { InlineKeyboardButton.WithCallbackData("Dev Desk 1 🖥️🖥️", "desk_241_1"), InlineKeyboardButton.WithCallbackData("Dev Desk 2 🖥️🖥️", "desk_241_2") },
                            new [] { InlineKeyboardButton.WithCallbackData("Dev Desk 3 🖥️🖥️", "desk_241_3"), InlineKeyboardButton.WithCallbackData("Dev Desk 4 🖥️🖥️", "desk_241_4") },
                            backButton
                        });
                        break;
                    case "301":
                        text = "🚪 Room 301 (Premium Workstations):\nEquipped with high-end wireless mice and noise-canceling headphones.";
                        deskKeyboard = new InlineKeyboardMarkup(new[] {
                            new [] { InlineKeyboardButton.WithCallbackData("Setup 1 🖥️🖱️🎧", "desk_301_1"), InlineKeyboardButton.WithCallbackData("Setup 2 🖥️🖱️🎧", "desk_301_2") },
                            new [] { InlineKeyboardButton.WithCallbackData("Setup 3 🖥️🖱️🎧", "desk_301_3") },
                            backButton
                        });
                        break;
                }

                await botClient.EditMessageText(chatId, messageId, text, replyMarkup: deskKeyboard, cancellationToken: cancellationToken);
                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                return;
            }

            // --- 2. Dynamic Desk Selection (Shows schedule and asks for time) ---
            if (callbackQuery.Data.StartsWith("desk_"))
            {
                await botClient.EditMessageText(chatId, messageId, "⏳ Verifying workspace availability...", cancellationToken: cancellationToken);

                // Extract the exact Workspace ID we passed in the previous step
                string deskIdStr = callbackQuery.Data.Replace("desk_", "");
                if (!Guid.TryParse(deskIdStr, out Guid deskId))
                {
                    await botClient.EditMessageText(chatId, messageId, "❌ Error: Invalid workspace ID.", cancellationToken: cancellationToken);
                    return;
                }

                try
                {
                    var workspaces = await ApiClient.GetFromJsonAsync<WorkspaceDto[]>("api/workspaces", cancellationToken);
                    var deskToBook = workspaces?.FirstOrDefault(w => w.Id == deskId);

                    if (deskToBook == null)
                    {
                        await botClient.EditMessageText(chatId, messageId, "❌ Error: Desk not found in the system.", cancellationToken: cancellationToken);
                        return;
                    }

                    var schedule = await ApiClient.GetFromJsonAsync<BookingDto[]>($"api/bookings/workspace/{deskToBook.Id}", cancellationToken);

                    string scheduleText = "";
                    if (schedule != null && schedule.Any())
                    {
                        scheduleText = "🔴 **Reserved slots (Upcoming):**\n";
                        foreach (var b in schedule)
                        {
                            scheduleText += $"• {b.StartTime.ToLocalTime():dd.MM} | {b.StartTime.ToLocalTime():HH:mm} - {b.EndTime.ToLocalTime():HH:mm}\n";
                        }
                    }
                    else
                    {
                        scheduleText = "🟢 **Currently fully available!**\n";
                    }

                    ActiveBookings[chatId] = new BookingState
                    {
                        WorkspaceId = deskToBook.Id,
                        WorkspaceName = deskToBook.Name,
                        Location = deskToBook.Location
                    };

                    string exampleDate = DateTime.Now.AddDays(1).ToString("dd.MM.yyyy");

                    string prompt = $"You selected **{deskToBook.Name}** in Room {deskToBook.Location}.\n\n" +
                                    $"{scheduleText}\n" +
                                    $"Please enter your booking date and time in this exact format:\n" +
                                    $"`DD.MM.YYYY HH:mm-HH:mm`\n\n" +
                                    $"Example: `{exampleDate} 10:00-14:00`";


                    await botClient.EditMessageText(chatId, messageId, prompt, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    await botClient.EditMessageText(chatId, messageId, $"❌ API Error: {ex.Message}", cancellationToken: cancellationToken);
                }

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            }
        }

        private static async Task ProcessTextMessage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var text = message.Text ?? "";

            // --- BUG FIX: Intercept main menu commands to reset states ---
            string[] mainMenuCommands = { "/start", "📅 Book Workspace", "🔍 Smart Search", "📋 My Bookings", "❌ Cancel Booking" };

            if (mainMenuCommands.Contains(text))
            {
                // User clicked a menu button, clear all active sessions immediately!
                ActiveSearches.Remove(chatId);
                ActiveBookings.Remove(chatId);
            }
            else
            {
                // Handle Smart Search Flow (Only if it is NOT a menu command)
                if (ActiveSearches.TryGetValue(chatId, out var searchState) && searchState.Step < 4)
                {
                    if (searchState.Step == 1 || searchState.Step == 3)
                    {
                        await HandleSmartSearchStepsAsync(botClient, chatId, text, searchState, cancellationToken);
                        return;
                    }
                    else if (searchState.Step == 2)
                    {
                        await botClient.SendMessage(chatId, "⚠️ Please use the buttons above to select amenities, or press Continue.", cancellationToken: cancellationToken);
                        return;
                    }
                }

                // Handle Standard Booking Flow (Only if it is NOT a menu command)
                if (ActiveBookings.TryGetValue(chatId, out var state))
                {
                    await HandleTimeInputAsync(botClient, chatId, text, state, cancellationToken);
                    return;
                }
            }

            // --- Main Menu Initialization ---
            var mainMenuKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "📅 Book Workspace", "🔍 Smart Search" },
                new KeyboardButton[] { "📋 My Bookings", "❌ Cancel Booking" }
            })
            { ResizeKeyboard = true };

            if (text == "/start")
            {
                await GetOrCreateUserIdAsync(chatId, message.Chat.FirstName ?? "User");
                await botClient.SendMessage(chatId, "Welcome to SmartOffice! 🏢\nPlease choose an action:", replyMarkup: mainMenuKeyboard, cancellationToken: cancellationToken);
                return;
            }

            if (text == "🔍 Smart Search")
            {
                ActiveSearches[chatId] = new SmartSearchState { Step = 1 };
                await botClient.SendMessage(chatId,
                    "Let's find the perfect spot! 🎯\n\n" +
                    "How many people need a workspace? (Enter a number, e.g., 1 or 4)",
                    cancellationToken: cancellationToken);
                return;
            }

            if (text == "📅 Book Workspace")
            {
                // 1. Делаем запрос к API за списком комнат
                var httpClient = new HttpClient(); // Или используй твой существующий ApiClient
                var locations = await httpClient.GetFromJsonAsync<List<string>>("https://localhost:7227/api/workspaces/locations");

                // 2. Динамически создаем кнопки (по 2 в ряд)
                var buttons = new List<List<InlineKeyboardButton>>();

                if (locations != null)
                {
                    for (int i = 0; i < locations.Count; i += 2)
                    {
                        var row = new List<InlineKeyboardButton>();

                        // Первая кнопка в ряду
                        row.Add(InlineKeyboardButton.WithCallbackData($"Room {locations[i]}", $"room_{locations[i]}"));

                        // Вторая кнопка в ряду (если есть)
                        if (i + 1 < locations.Count)
                        {
                            row.Add(InlineKeyboardButton.WithCallbackData($"Room {locations[i + 1]}", $"room_{locations[i + 1]}"));
                        }

                        buttons.Add(row);
                    }
                }

                var inlineKeyboard = new InlineKeyboardMarkup(buttons);

                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Please select a room to see available workspaces:",
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken);

                return;
            }

            if (text == "📋 My Bookings") { await HandleMyBookingsAsync(botClient, chatId, cancellationToken); return; }
            if (text == "❌ Cancel Booking") { await HandleCancelMenuAsync(botClient, chatId, cancellationToken); return; }

            await botClient.SendMessage(chatId, "Please use the buttons below.", replyMarkup: mainMenuKeyboard, cancellationToken: cancellationToken);
        }

        private static async Task HandleSmartSearchStepsAsync(ITelegramBotClient botClient, long chatId, string input, SmartSearchState state, CancellationToken cancellationToken)
        {
            // STEP 1: Capacity
            if (state.Step == 1)
            {
                if (int.TryParse(input, out int capacity) && capacity > 0)
                {
                    state.Capacity = capacity;
                    state.Step = 2; // Move to Step 2 (Amenities Checkboxes)

                    await botClient.SendMessage(chatId,
                        $"✅ Space for {capacity} person(s).\n\n" +
                        "Select required equipment (Tap to toggle, then press Continue):",
                        replyMarkup: GetAmenitiesKeyboard(state), cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessage(chatId, "⚠️ Please enter a valid number (e.g., 1, 4, 10):", cancellationToken: cancellationToken);
                }
                return;
            }

            // STEP 3: Time and Search Database
            if (state.Step == 3)
            {
                try
                {
                    var parts = input.Split(' ');
                    if (parts.Length != 2) throw new FormatException();
                    var timeParts = parts[1].Split('-');
                    if (timeParts.Length != 2) throw new FormatException();

                    var format = "dd.MM.yyyy HH:mm";
                    var parsedStart = DateTime.ParseExact($"{parts[0]} {timeParts[0]}", format, CultureInfo.InvariantCulture);
                    var parsedEnd = DateTime.ParseExact($"{parts[0]} {timeParts[1]}", format, CultureInfo.InvariantCulture);

                    if (parsedStart.TimeOfDay < TimeSpan.FromHours(8) || parsedEnd.TimeOfDay > TimeSpan.FromHours(20))
                    {
                        await botClient.SendMessage(chatId, "⚠️ Working hours are from 08:00 to 20:00. Please try again:", cancellationToken: cancellationToken);
                        return;
                    }

                    var startTime = parsedStart.ToUniversalTime();
                    var endTime = parsedEnd.ToUniversalTime();

                    if (startTime >= endTime || startTime < DateTime.UtcNow)
                    {
                        await botClient.SendMessage(chatId, "⚠️ Invalid time range. Please try again:", cancellationToken: cancellationToken);
                        return;
                    }

                    state.StartTime = startTime;
                    state.EndTime = endTime;
                    state.Step = 4; // Move to confirmation step

                    // Build query string for multiple amenities
                    string amenitiesQuery = "";
                    if (state.SelectedAmenities.Any())
                    {
                        amenitiesQuery = "&" + string.Join("&", state.SelectedAmenities.Select(a => $"amenities={Uri.EscapeDataString(a)}"));
                    }

                    string url = $"api/workspaces/search?start={startTime:O}&end={endTime:O}&capacity={state.Capacity}{amenitiesQuery}";

                    var response = await ApiClient.GetAsync(url, cancellationToken);
                    var searchResults = await response.Content.ReadFromJsonAsync<WorkspaceSearchResultDto[]>(cancellationToken: cancellationToken);

                    if (searchResults == null || !searchResults.Any())
                    {
                        ActiveSearches.Remove(chatId);
                        await botClient.SendMessage(chatId, "😔 Sorry, no workspaces matching your criteria are available. Please try different parameters.", cancellationToken: cancellationToken);
                        return;
                    }

                    // Format message with Matches and Missings
                    string resultText = "✨ **Search Results:**\n\n";
                    var inlineButtons = new List<InlineKeyboardButton[]>();

                    foreach (var w in searchResults)
                    {
                        string matchText = w.MatchedAmenities != null && w.MatchedAmenities.Any() ? $"✅ Has: {string.Join(", ", w.MatchedAmenities)}" : "";
                        string missText = w.MissingAmenities != null && w.MissingAmenities.Any() ? $"❌ Missing: {string.Join(", ", w.MissingAmenities)}" : "";

                        // --- NEW: Dynamic Load Warning Message ---
                        string loadWarning = w.IsHighLoad
                            ? $"⚠️ **High Load Alert ({w.OccupancyPercentage}% full)!**\n_Room is heavily booked. It might be noisy. Consider a different time or room._"
                            : $"🟢 **Quiet Zone** (Current load: {w.OccupancyPercentage}%)";

                        resultText += $"🏢 **{w.Name}** (Room {w.Location})\n";
                        resultText += $"{loadWarning}\n";

                        if (!string.IsNullOrEmpty(matchText)) resultText += $"{matchText}\n";
                        if (!string.IsNullOrEmpty(missText)) resultText += $"{missText}\n";
                        resultText += "\n";

                        inlineButtons.Add(new[] { InlineKeyboardButton.WithCallbackData($"Book {w.Name}", $"smartbook_{w.Id}") });
                    }
                    inlineButtons.Add(new[] { InlineKeyboardButton.WithCallbackData("❌ Cancel", "stop_search") });

                    await botClient.SendMessage(chatId, resultText, replyMarkup: new InlineKeyboardMarkup(inlineButtons), parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                }
                catch (FormatException)
                {
                    await botClient.SendMessage(chatId, "❌ Invalid format. Use `DD.MM.YYYY HH:mm-HH:mm`", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                }
            }
        }

        private static async Task HandleMyBookingsAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            try
            {
                var dbUserId = await GetOrCreateUserIdAsync(chatId, "User");
                var bookings = await ApiClient.GetFromJsonAsync<BookingDto[]>($"api/bookings/my/{dbUserId}", cancellationToken);

                if (bookings == null || !bookings.Any())
                {
                    await botClient.SendMessage(chatId, "You don't have any active bookings.", cancellationToken: cancellationToken);
                    return;
                }

                await botClient.SendMessage(chatId, "📋 **Your Active Bookings:**", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);

                foreach (var b in bookings)
                {
                    string status = b.IsCheckInConfirmed ? "✅ Confirmed" : "⏳ Pending Check-in";
                    string text = $"🏢 **{b.Workspace.Name}** (Room {b.Workspace.Location})\n" +
                                  $"📅 {b.StartTime.ToLocalTime():dd.MM.yyyy} | ⏰ {b.StartTime.ToLocalTime():HH:mm} - {b.EndTime.ToLocalTime():HH:mm}\n" +
                                  $"📌 Status: {status}";

                    var buttons = new List<InlineKeyboardButton[]>();

                    // Show Check-in button only if not confirmed yet
                    if (!b.IsCheckInConfirmed)
                    {
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("📍 Check-In", $"checkin_{b.Id}") });
                    }
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("❌ Cancel Booking", $"cancel_{b.Id}") });

                    await botClient.SendMessage(chatId, text, replyMarkup: new InlineKeyboardMarkup(buttons), parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                }
            }
            catch (Exception)
            {
                await botClient.SendMessage(chatId, "❌ Could not fetch bookings. Check if API is running.", cancellationToken: cancellationToken);
            }
        }
        private static async Task HandleCancelMenuAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            try
            {
                var dbUserId = await GetOrCreateUserIdAsync(chatId, "User");
                var bookings = await ApiClient.GetFromJsonAsync<BookingDto[]>($"api/bookings/my/{dbUserId}", cancellationToken);

                if (bookings == null || !bookings.Any())
                {
                    await botClient.SendMessage(chatId, "You don't have any active bookings to cancel.", cancellationToken: cancellationToken);
                    return;
                }

                var inlineKeyboard = new List<InlineKeyboardButton[]>();
                foreach (var b in bookings)
                {
                    var buttonText = $"❌ {b.Workspace.Name} ({b.StartTime.ToLocalTime():dd.MM HH:mm})";
                    inlineKeyboard.Add(new[] { InlineKeyboardButton.WithCallbackData(buttonText, $"cancel_{b.Id}") });
                }

                await botClient.SendMessage(chatId, "Select a booking to cancel:", replyMarkup: new InlineKeyboardMarkup(inlineKeyboard), cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
                await botClient.SendMessage(chatId, "❌ Could not fetch bookings.", cancellationToken: cancellationToken);
            }
        }

        private static async Task HandleTimeInputAsync(ITelegramBotClient botClient, long chatId, string input, BookingState state, CancellationToken cancellationToken)
        {
            try
            {
                var parts = input.Split(' ');
                if (parts.Length != 2) throw new FormatException();

                var datePart = parts[0];
                var timeParts = parts[1].Split('-');
                if (timeParts.Length != 2) throw new FormatException();
                var startStr = $"{datePart} {timeParts[0]}";
                var endStr = $"{datePart} {timeParts[1]}";

                var format = "dd.MM.yyyy HH:mm";

                var parsedStart = DateTime.ParseExact(startStr, format, CultureInfo.InvariantCulture);
                var parsedEnd = DateTime.ParseExact(endStr, format, CultureInfo.InvariantCulture);

                if (parsedStart.TimeOfDay < TimeSpan.FromHours(8) || parsedEnd.TimeOfDay > TimeSpan.FromHours(20))
                {
                    await botClient.SendMessage(chatId, "⚠️ Working hours are from 08:00 to 20:00. Please enter a valid time within this range:", cancellationToken: cancellationToken);
                    return;
                }

                var startTime = parsedStart.ToUniversalTime();
                var endTime = parsedEnd.ToUniversalTime();

                if (startTime >= endTime || startTime < DateTime.UtcNow)
                {
                    await botClient.SendMessage(chatId, "⚠️ Invalid time range. Start time must be in the future and before end time.\nPlease try typing it again:", cancellationToken: cancellationToken);
                    return;
                }
                var dbUserId = await GetOrCreateUserIdAsync(chatId, "User");
                var bookRequest = new BookRequest
                {
                    WorkspaceId = state.WorkspaceId,
                    UserId = dbUserId,
                    StartTime = startTime,
                    EndTime = endTime
                };

                var response = await ApiClient.PostAsJsonAsync("api/workspaces/book", bookRequest, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    ActiveBookings.Remove(chatId); // clear state on success

                    await botClient.SendMessage(chatId,
                        $"✅ **Booking Confirmed!**\n\n" +
                        $"🏢 Workspace: {state.WorkspaceName}\n" +
                        $"🚪 Room: {state.Location}\n" +
                        $"📅 Date: {startTime.ToLocalTime():dd.MM.yyyy}\n" +
                        $"⏰ Time: {startTime.ToLocalTime():HH:mm} - {endTime.ToLocalTime():HH:mm}",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendMessage(chatId, "⚠️ Booking failed: This workspace is already booked for the selected time. Please try a different time:", cancellationToken: cancellationToken);
                }
            }
            catch (FormatException)
            {
                await botClient.SendMessage(chatId, "❌ Invalid format. Please use `DD.MM.YYYY HH:mm-HH:mm`\nExample: `06.05.2026 10:00-14:00`", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
                ActiveBookings.Remove(chatId); // abort on critical error
                await botClient.SendMessage(chatId, "❌ An error occurred processing your request. Please start over.", cancellationToken: cancellationToken);
            }
        }

        private static async Task<Guid> GetOrCreateUserIdAsync(long chatId, string firstName)
        {
            if (UserCache.TryGetValue(chatId, out var userId))
                return userId;

            var payload = new { TelegramId = chatId, Name = firstName };
            var response = await ApiClient.PostAsJsonAsync("api/users/sync", payload);
            response.EnsureSuccessStatusCode();

            var user = await response.Content.ReadFromJsonAsync<UserDto>();
            UserCache[chatId] = user!.Id;

            return user.Id;
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine(exception.ToString());
            return Task.CompletedTask;
        }
    }

    public enum Amenities
    {
        None = 0,
        Projector = 1,
        Whiteboard = 2,
        Monitor = 4
    }

    public class WorkspaceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public Dictionary<Amenities, int> Amenities { get; set; } = new Dictionary<Amenities, int>();
    }

    public class BookRequest
    {
        public Guid WorkspaceId { get; set; }
        public Guid UserId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class BookingState
    {
        public Guid WorkspaceId { get; set; }
        public string WorkspaceName { get; set; }
        public string Location { get; set; }
    }

    public class BookingDto
    {
        public Guid Id { get; set; }
        public WorkspaceDto Workspace { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsCheckInConfirmed { get; set; }
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public long TelegramId { get; set; }
        public string Name { get; set; }
    }

    public class SmartSearchState
    {
        public int Step { get; set; } = 1;
        public int Capacity { get; set; }
        public List<string> SelectedAmenities { get; set; } = new List<string>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

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