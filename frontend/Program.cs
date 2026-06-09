using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using YouthGroupAttendance.Frontend;
using YouthGroupAttendance.Frontend.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<AuthState>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp =>
{
    var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    return http;
});
builder.Services.AddScoped<ApiService>();

await builder.Build().RunAsync();
