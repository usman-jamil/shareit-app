using Microsoft.AspNetCore.Http.HttpResults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

WebApplication app = builder.Build();
app.MapOpenApi();

app.UseHttpsRedirection();

await app.RunAsync();
