using TreeEditor.Domain;
using TreeEditor.Domain.Models;

namespace TreeEditor.Api.Services
{
    // Scoped wrapper that delegates to a per-client ElementCache instance stored in ElementCacheStore.
    public class PerUserElementCache : IElementCache
    {
        private readonly ElementCacheStore _store;
        private readonly IHttpContextAccessor _ctxAccessor;
        private readonly Microsoft.Extensions.Logging.ILogger<PerUserElementCache>? _logger;

        public PerUserElementCache(ElementCacheStore store, IHttpContextAccessor ctxAccessor, Microsoft.Extensions.Logging.ILogger<PerUserElementCache>? logger = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _ctxAccessor = ctxAccessor ?? throw new ArgumentNullException(nameof(ctxAccessor));
            _logger = logger;
        }

        // Resolve ElementCache and client key for the current request. If client id missing or invalid,
        // throw UnauthorizedAccessException so callers (and middleware) can direct client to request/renew a key.
        private (ElementCache cache, string clientId, KeyStore? ks) ResolveCacheAndKey()
        {
            var ctx = _ctxAccessor.HttpContext;
            if (ctx == null) throw new InvalidOperationException("No HttpContext available");

            string? id = null;
            if (ctx.Request.Headers.TryGetValue("X-Client-Id", out var hdr) && !string.IsNullOrEmpty(hdr))
            {
                id = hdr.ToString();
            }

            var ks = ctx.RequestServices.GetService(typeof(KeyStore)) as KeyStore;
            // If KeyStore is configured, require a valid id for any cache operation.
            if (ks != null)
            {
                if (string.IsNullOrEmpty(id) || !ks.Validate(id))
                {
                    // invalid or missing key -> signal unauthorized so frontend can redirect / notify user
                    throw new UnauthorizedAccessException("Client key missing or expired");
                }
            }

            if (string.IsNullOrEmpty(id))
            {
                // If no KeyStore present, allow transient ids (not persisted) but still create cache entry
                id = Guid.NewGuid().ToString();
            }

            try { _logger?.LogDebug("PerUserElementCache.ResolveCacheAndKey: X-Client-Id='{ClientId}' Path='{Path}'", id ?? "<null>", ctx.Request.Path); } catch { }

            var cache = _store.GetOrCreate(id);
            return (cache, id, ks);
        }

        public Task<bool> LoadToCacheAsync(Element element)
        {
            var (cache, id, ks) = ResolveCacheAndKey();
            var t = cache.LoadToCacheAsync(element);
            return t.ContinueWith(tt =>
            {
                if (tt.Status == TaskStatus.RanToCompletion && tt.Result && ks != null)
                {
                    try { ks.Refresh(id); } catch { }
                }
                return tt.Result;
            });
        }

        public List<CachedElement> GetAllCached()
        {
            var (cache, id, ks) = ResolveCacheAndKey();
            var list = cache.GetAllCached();
            try { if (ks != null) ks.Refresh(id); } catch { }
            return list;
        }

        public bool EditCached(int id, string value)
        {
            var (cache, clientId, ks) = ResolveCacheAndKey();
            var ok = cache.EditCached(id, value);
            try { if (ok && ks != null) ks.Refresh(clientId); } catch { }
            return ok;
        }

        public CachedElement AddCachedChild(int parentId, string value)
        {
            var (cache, clientId, ks) = ResolveCacheAndKey();
            var ce = cache.AddCachedChild(parentId, value);
            try { if (ce != null && ks != null) ks.Refresh(clientId); } catch { }
            return ce;
        }

        public bool DeleteCached(int id)
        {
            var (cache, clientId, ks) = ResolveCacheAndKey();
            var ok = cache.DeleteCached(id);
            try { if (ok && ks != null) ks.Refresh(clientId); } catch { }
            return ok;
        }

        public Task ApplyAsync(AppDbContext db)
        {
            var (cache, clientId, ks) = ResolveCacheAndKey();
            return cache.ApplyAsync(db).ContinueWith(tt =>
            {
                if (tt.Status == TaskStatus.RanToCompletion && ks != null)
                {
                    try { ks.Refresh(clientId); } catch { }
                }
                if (tt.IsFaulted) throw tt.Exception!;
            });
        }

        public void Clear()
        {
            var (cache, clientId, ks) = ResolveCacheAndKey();
            cache.Clear();
            try { if (ks != null) ks.Refresh(clientId); } catch { }
        }
    }
}
