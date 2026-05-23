using Microsoft.AspNetCore.Mvc;
using SmartOffice.Web.Models;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using SmartOffice.Api.Enums;

namespace SmartOffice.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("ApiClient");

            var workspaces = await client.GetFromJsonAsync<List<WorkspaceViewModel>>("api/workspaces");

            return View(workspaces ?? new List<WorkspaceViewModel>());
        }

        // 1. Method for displaying a blank form (GET)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 2. Method for submitting form data to API (POST)
        [HttpPost]
        public async Task<IActionResult> Create(WorkspaceViewModel model)
        {
            ModelState.Remove("Amenities");

            if (!ModelState.IsValid) return View(model);

            model.Amenities = new Dictionary<Amenities, int>();
            if (model.HasProjector) model.Amenities[Amenities.Projector] = 1;
            if (model.HasWhiteboard) model.Amenities[Amenities.Whiteboard] = 1;
            if (model.HasMonitor) model.Amenities[Amenities.Monitor] = 1;

            var payload = new
            {
                Name = model.Name,
                Location = model.Location,
                Capacity = model.Capacity, // Sending to API
                Amenities = model.Amenities
            };

            var client = _httpClientFactory.CreateClient("ApiClient");
            var response = await client.PostAsJsonAsync("api/workspaces", payload);

            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction("Index");
            }

           
            var apiError = await response.Content.ReadAsStringAsync();
            apiError = apiError.Trim('"');

            string displayMessage = string.IsNullOrWhiteSpace(apiError)
                ? "An error occurred while creating the workspace."
                : apiError;

            ModelState.AddModelError(string.Empty, displayMessage);
            return View(model);
        }
        public async Task<IActionResult> Dashboard()
        {
            var client = _httpClientFactory.CreateClient("ApiClient");
            var stats = await client.GetFromJsonAsync<DashboardViewModel>("api/bookings/statistics");

            return View(stats ?? new DashboardViewModel());
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // --- DELETE WORKSPACE ---
        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = _httpClientFactory.CreateClient("ApiClient");
            await client.DeleteAsync($"api/workspaces/{id}");
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var client = _httpClientFactory.CreateClient("ApiClient");
            var workspaces = await client.GetFromJsonAsync<List<WorkspaceViewModel>>("api/workspaces");
            var workspace = workspaces?.FirstOrDefault(w => w.Id == id);

            if (workspace == null) return NotFound();

            // Unpack Dictionary from API into boolean values for UI checkboxes
            if (workspace.Amenities != null)
            {
                workspace.HasProjector = workspace.Amenities.ContainsKey(Amenities.Projector);
                workspace.HasWhiteboard = workspace.Amenities.ContainsKey(Amenities.Whiteboard);
                workspace.HasMonitor = workspace.Amenities.ContainsKey(Amenities.Monitor);
            }

            return View(workspace);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(WorkspaceViewModel model)
        {
            ModelState.Remove("Amenities");

            if (!ModelState.IsValid) return View(model);

            model.Amenities = new Dictionary<Amenities, int>();
            if (model.HasProjector) model.Amenities[Amenities.Projector] = 1;
            if (model.HasWhiteboard) model.Amenities[Amenities.Whiteboard] = 1;
            if (model.HasMonitor) model.Amenities[Amenities.Monitor] = 1;

            var payload = new
            {
                Name = model.Name,
                Location = model.Location,
                Capacity = model.Capacity, // Sending to API
                Amenities = model.Amenities
            };

            var client = _httpClientFactory.CreateClient("ApiClient");
            var response = await client.PutAsJsonAsync($"api/workspaces/{model.Id}", payload);

            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction("Index");
            }

            var apiError = await response.Content.ReadAsStringAsync();
            apiError = apiError.Trim('"');

            string displayMessage = string.IsNullOrWhiteSpace(apiError)
                ? "An error occurred while updating the workspace."
                : apiError;

            ModelState.AddModelError(string.Empty, displayMessage);
            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> Dashboard(string period = "all")
        {
            var client = _httpClientFactory.CreateClient("ApiClient");

            var stats = await client.GetFromJsonAsync<DashboardViewModel>($"api/bookings/statistics?period={period}");

            if (stats == null)
            {
                return View(new DashboardViewModel());
            }

            stats.SelectedPeriod = period;

            return View(stats);
        }
    }
}