using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Json(new { service = "UpstreamB", status = "ok" }));
app.MapGet("/hello", () => Results.Json(new { message = "Hello from Upstream B" }));
app.MapGet("/health", () => Results.Ok("healthy"));

await app.RunAsync();
