using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Json(new { service = "UpstreamA", status = "ok" }));
app.MapGet("/hello", () => Results.Json(new { message = "Hello from Upstream A" }));
app.MapGet("/health", () => Results.Ok("healthy"));

await app.RunAsync();
