using TreeEditor.Api.Services;
using TreeEditor.Domain;
using TreeViewer.DTO.Node;
using Microsoft.EntityFrameworkCore;
using TreeEditor.Domain.Models;


namespace TreeEditor.Api.Modules
{
    public class ApiCacheModule
    {
        public static void RegisterApiCacheModule(WebApplication app)
        {
            app.MapGet("/api/children", async (int? parentId, AppDbContext db) =>
            {
                var children = await db.Elements
                    .Where(e => e.ParentId == parentId && !e.IsDeleted)
                    .Select(e => new { e.Id, e.ParentId, e.Value })
                    .ToListAsync();
                return Results.Ok(children);
            });

            app.MapGet("/api/element/{id}", async (int id, AppDbContext db) =>
            {
                var el = await db.Elements.FindAsync(id);
                if (el == null || el.IsDeleted) return Results.NotFound();
                return Results.Ok(new { el.Id, el.ParentId, el.Value });
            });

            app.MapPost("/api/cache/load/{id}", async (int id, IElementCache cache, AppDbContext db) =>
            {
                var el = await db.Elements.FindAsync(id);
                if (el == null || el.IsDeleted) return Results.NotFound();
                var ok = await cache.LoadToCacheAsync(el);
                return ok ? Results.Ok() : Results.Conflict();
            });

            app.MapGet("/api/cache", (IElementCache cache) =>
            {
                var list = cache.GetAllCached();
                return Results.Ok(list);
            });

            app.MapPut("/api/cache/{id}", (int id, EditNode dto, IElementCache cache) =>
            {
                var ok = cache.EditCached(id, dto.Value);
                return ok ? Results.Ok() : Results.NotFound();
            });

            app.MapPost("/api/cache/{parentId}/add", (int parentId, AddNode dto, IElementCache cache) =>
            {
                var ce = cache.AddCachedChild(parentId, dto.Value);
                return Results.Ok(ce);
            });

            app.MapDelete("/api/cache/{id}", (int id, IElementCache cache) =>
            {
                var ok = cache.DeleteCached(id);
                return ok ? Results.Ok() : Results.NotFound();
            });

            app.MapPost("/api/cache/apply", async (IElementCache cache, AppDbContext db) =>
            {
                await cache.ApplyAsync(db);
                return Results.Ok();
            });

            app.MapPost("/api/reset", async (IElementCache cache, ElementCacheStore store, AppDbContext db) =>
            {
                // delete and recreate using explicit seeding with stable IDs by recreating table
                // Use transaction to ensure consistent state

                using var tx = await db.Database.BeginTransactionAsync();

                // remove all rows
                db.Elements.RemoveRange(db.Elements);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name = 'Elements';");

                await db.SaveChangesAsync();

                var root = new Element { Value = "Root", ParentId = null };
                db.Elements.Add(root);
                await db.SaveChangesAsync();

                var childA = new Element { Value = "Child A", ParentId = root.Id };
                var childB = new Element { Value = "Child B", ParentId = root.Id };
                db.Elements.AddRange(childA, childB);
                await db.SaveChangesAsync();

                var a1 = new Element { Value = "Child A.1", ParentId = childA.Id };
                db.Elements.Add(a1);
                await db.SaveChangesAsync();

                var a1a = new Element { Value = "Child A.1.a", ParentId = a1.Id };
                db.Elements.Add(a1a);
                await db.SaveChangesAsync();

                await tx.CommitAsync();

                // Clear all server caches and persisted per-client caches so reset is global
                try { store.ClearAll(); } catch { }

                return Results.Ok();
            });
        }
    }
}
