using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TreeEditor.Api.Services;
using TreeEditor.Domain;
using TreeEditor.Domain.Models;
using Xunit;

namespace TreeEditor.Api.Tests
{
    public class ElementCacheTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public ElementCacheTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var db = new AppDbContext(_options);
            db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        [Fact]
        public async Task Apply_AddNewChild_PersistsWithParent()
        {
            using var db = new AppDbContext(_options);
            var root = new Element { Value = "Root", ParentId = null };
            db.Elements.Add(root);
            await db.SaveChangesAsync();

            var cache = new ElementCache();
            var temp = cache.AddCachedChild(root.Id, "NewChild");
            await cache.ApplyAsync(db);

            using var db2 = new AppDbContext(_options);
            var added = db2.Elements.FirstOrDefault(e => e.Value == "NewChild");
            Assert.NotNull(added);
            Assert.Equal(root.Id, added.ParentId);
        }

        [Fact]
        public async Task Apply_EditExisting_UpdatesValue()
        {
            using var db = new AppDbContext(_options);
            var root = new Element { Value = "Root", ParentId = null };
            db.Elements.Add(root);
            await db.SaveChangesAsync();

            var cache = new ElementCache();
            // simulate loading into cache
            var el = await db.Elements.FindAsync(root.Id);
            Assert.NotNull(el);
            await cache.LoadToCacheAsync(el!);
            var ok = cache.EditCached(root.Id, "RootUpdated");
            Assert.True(ok);

            await cache.ApplyAsync(db);
            using var db2 = new AppDbContext(_options);
            var updated = await db2.Elements.FindAsync(root.Id);
            Assert.Equal("RootUpdated", updated!.Value);
        }

        [Fact]
        public async Task Apply_DeleteExisting_MarksSubtreeDeleted()
        {
            using var db = new AppDbContext(_options);
            var root = new Element { Value = "Root", ParentId = null };
            db.Elements.Add(root);
            await db.SaveChangesAsync();

            var childA = new Element { Value = "ChildA", ParentId = root.Id };
            db.Elements.Add(childA);
            await db.SaveChangesAsync();

            var a1 = new Element { Value = "A1", ParentId = childA.Id };
            db.Elements.Add(a1);
            await db.SaveChangesAsync();

            var cache = new ElementCache();
            // Mark childA as deleted in cache
            // Need to load childA into cache first
            await cache.LoadToCacheAsync(childA);
            var delOk = cache.DeleteCached(childA.Id);
            Assert.True(delOk);

            await cache.ApplyAsync(db);

            using var db2 = new AppDbContext(_options);
            var reChildA = await db2.Elements.FindAsync(childA.Id);
            var reA1 = await db2.Elements.FindAsync(a1.Id);
            Assert.True(reChildA!.IsDeleted);
            Assert.True(reA1!.IsDeleted);
        }
    }
}
