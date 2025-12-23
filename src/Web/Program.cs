using Adapters.ApiClient;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Web;
using Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Default HttpClient for static assets
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register API clients
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5001";
var apiKey = builder.Configuration["ApiKey"] ?? "your-api-key-here";

builder.Services.AddPrescriptionOrderApiClients(apiBaseUrl, apiKey);

// Register business logic services
builder.Services.AddScoped<OrderService>();

await builder.Build().RunAsync();
