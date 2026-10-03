using Microsoft.EntityFrameworkCore;
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
app.UseCors();

// Enable Swagger middleware
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ensure database and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Seed();
}

ApiCacheModule.RegisterApiCacheModule(app);

app.Run();
