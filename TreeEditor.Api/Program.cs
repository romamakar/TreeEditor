using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TreeEditor.Api.Modules;
using TreeEditor.Api.Services;
using TreeEditor.Domain;

var builder = WebApplication.CreateBuilder(args);

// Add Swagger/OpenAPI for API browsing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=tree.db"));
// Per-user caches: use a store singleton and a scoped wrapper that selects the cache by client id (cookie)
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<TreeEditor.Api.Services.ElementCacheStore>();
builder.Services.AddScoped<IElementCache, TreeEditor.Api.Services.PerUserElementCache>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (UnauthorizedAccessException)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized");
    }
});
app.UseCors();

// Enable Swagger middleware
app.UseSwagger();
app.UseSwaggerUI();

//Enable Scalar API Reference middleware
app.MapSwagger("/openapi/{documentName}.json");
app.MapScalarApiReference();


// Ensure database and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Seed();
}

ApiCacheModule.RegisterApiCacheModule(app);

app.Run();
