using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DemoApp;
using DemoApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register API settings service first (needed by handler)
builder.Services.AddScoped<ApiSettingsService>();

// Register HttpClient - Blazor WASM uses browser's Fetch API
builder.Services.AddScoped(sp => new HttpClient());

// Register API client
builder.Services.AddScoped<ApiClient>();

await builder.Build().RunAsync();
