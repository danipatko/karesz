using karesz;
using karesz.Runner;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var httpClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddScoped(_ => httpClient);
builder.Services.AddFluentUIComponents();

builder.Services
    .AddSingleton<WorkspaceService>()
    .AddSingleton<CompilerSerivce>()
    .AddSingleton(sp => (IJSInProcessRuntime)sp.GetRequiredService<IJSRuntime>());

_ = Task.Run(async () =>
{
    await WorkspaceService.InitAsync(httpClient);
    await CompilerSerivce.InitAsync(WorkspaceService.BasicReferenceAssemblies);
});

await builder.Build().RunAsync();
