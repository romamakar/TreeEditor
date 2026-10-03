using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TreeEditor.Domain;
using TreeEditor.Domain.Models;

namespace TreeEditor.Api.Services
{
    public class ElementCache : IElementCache
    {
        // cached elements: includes loaded (positive Ids) and newly added (negative temp Ids)
        private readonly ConcurrentDictionary<int, CachedElement> _cache = new();
        private int _nextTempId = -1;

        public Task<bool> LoadToCacheAsync(Element element)
        {
            if (element == null || element.IsDeleted) return Task.FromResult(false);
            var ce = new CachedElement
            {
                Id = element.Id,
                ParentId = element.ParentId,
                Value = element.Value,
                IsDeleted = false
            };
            _cache[element.Id] = ce;
            return Task.FromResult(true);
        }

        public List<CachedElement> GetAllCached()
        {
            // return a snapshot
            return _cache.Values.OrderBy(e => e.Id).Select(e => new CachedElement
            {
                Id = e.Id,
                ParentId = e.ParentId,
                Value = e.Value,
                IsDeleted = e.IsDeleted
            }).ToList();
        }

        public bool EditCached(int id, string value)
        {
            if (!_cache.TryGetValue(id, out var ce) || ce.IsDeleted) return false;
            ce.Value = value;
            return true;
        }

        public CachedElement AddCachedChild(int parentId, string value)
        {
            var id = Interlocked.Decrement(ref _nextTempId);
            var ce = new CachedElement
            {
                Id = id,
                ParentId = parentId,
                Value = value,
                IsDeleted = false
            };
            _cache[id] = ce;
            return ce;
        }

        public bool DeleteCached(int id)
        {
            if (!_cache.TryGetValue(id, out var ce)) return false;
            // mark as deleted. Also mark all cached descendants as deleted
            MarkDeletedRecursive(id);
            return true;
        }

        private void MarkDeletedRecursive(int id)
        {
            if (_cache.TryGetValue(id, out var ce))
            {
                ce.IsDeleted = true;
                var children = _cache.Values.Where(c => c.ParentId == id).Select(c => c.Id).ToList();
                foreach (var childId in children)
                    MarkDeletedRecursive(childId);
            }
        }

        private readonly ILogger<ElementCache>? _logger;

        public ElementCache(ILogger<ElementCache>? logger = null)
        {
            _logger = logger;
        }

        public async Task ApplyAsync(AppDbContext db)
        {
            // Apply new elements first (negative ids)
            var newElements = _cache.Values.Where(e => e.Id < 0 && !e.IsDeleted).ToList();

            // map temp id -> new db id
            var idMap = new Dictionary<int, int>();
            // We need to insert parents before children when both are new. Do iterative insertion passes.
            var pending = newElements.ToDictionary(e => e.Id, e => e);

            while (pending.Count > 0)
            {
                var insertedThisRound = new List<int>();
                foreach (var kv in pending)
                {
                    var tempId = kv.Key;
                    var el = kv.Value;
                    // if parent is null or parent is existing (>=1) or parent is already mapped, we can insert
                    if (el.ParentId == null || (el.ParentId >= 1) || (el.ParentId < 0 && idMap.ContainsKey(el.ParentId.Value)))
                    {
                        int? parentResolved = null;
                        if (el.ParentId != null)
                        {
                            if (el.ParentId >= 1)
                                parentResolved = el.ParentId;
                            else
                                parentResolved = idMap[el.ParentId.Value];
                        }

                        var dbEl = new Element
                        {
                            ParentId = parentResolved,
                            Value = el.Value,
                            IsDeleted = false
                        };
                        db.Elements.Add(dbEl);
                        await db.SaveChangesAsync();
                        idMap[tempId] = dbEl.Id;
                        insertedThisRound.Add(tempId);
                    }
                }
                if (insertedThisRound.Count == 0)
                {
                    // circular or missing parent mapping - break
                    break;
                }
                foreach (var id in insertedThisRound)
                    pending.Remove(id);
            }

            // Apply edits to existing DB elements
            var edits = _cache.Values.Where(e => e.Id > 0 && !e.IsDeleted).ToList();
            foreach (var e in edits)
            {
                var dbEl = await db.Elements.FindAsync(e.Id);
                if (dbEl != null && !dbEl.IsDeleted)
                {
                    if (dbEl.Value != e.Value)
                    {
                        dbEl.Value = e.Value;
                        db.Elements.Update(dbEl);
                    }
                }
            }

            await db.SaveChangesAsync();

            // Handle deletions: for every cached element marked IsDeleted with positive id -> mark subtree deleted in DB
            var deletedRoots = _cache.Values.Where(e => e.IsDeleted && e.Id > 0).Select(e => e.Id).ToList();
            foreach (var rootId in deletedRoots)
            {
                // recursive CTE to find subtree
                var sql = @"WITH RECURSIVE subtree(id) AS (
    SELECT Id FROM Elements WHERE Id = @root
  UNION ALL
    SELECT e.Id FROM Elements e JOIN subtree s ON e.ParentId = s.id
)
UPDATE Elements SET IsDeleted = 1 WHERE Id IN (SELECT id FROM subtree);";
                var affected = await db.Database.ExecuteSqlRawAsync(sql, new Microsoft.Data.Sqlite.SqliteParameter("@root", rootId));
                try { _logger?.LogInformation("ElementCache.ApplyAsync: delete subtree root={RootId}, affected={Affected}", rootId, affected); } catch {}
            }

            // For new elements that were marked deleted in cache (never persisted), nothing to do (they were never created)

            await db.SaveChangesAsync();

            // Clear cache after apply
            _cache.Clear();
            _nextTempId = -1;
        }

        public void Clear()
        {
            _cache.Clear();
            _nextTempId = -1;
        }
    }
}
