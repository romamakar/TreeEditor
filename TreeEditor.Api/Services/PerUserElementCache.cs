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
        private const string CookieName = "tree-client-id";

        public PerUserElementCache(ElementCacheStore store, IHttpContextAccessor ctxAccessor, Microsoft.Extensions.Logging.ILogger<PerUserElementCache>? logger = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _ctxAccessor = ctxAccessor ?? throw new ArgumentNullException(nameof(ctxAccessor));
            _logger = logger;
        }

        private ElementCache GetCache()
        {
            var ctx = _ctxAccessor.HttpContext;
            if (ctx == null) throw new InvalidOperationException("No HttpContext available");
            // Expect a client id header provided by the frontend (no cookies)
            string? id = null;
            if (ctx.Request.Headers.TryGetValue("X-Client-Id", out var hdr) && !string.IsNullOrEmpty(hdr))
            {
                id = hdr.ToString();
            }
            // debug log header and request path
            try { _logger?.LogDebug("PerUserElementCache.GetCache: X-Client-Id='{ClientId}' Path='{Path}'", id ?? "<null>", ctx.Request.Path); } catch {}
            // If header not provided, fall back to a server-generated id (transient) but do not set cookies
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
            }
            return _store.GetOrCreate(id);
        }

        public Task<bool> LoadToCacheAsync(Element element)
        {
            return GetCache().LoadToCacheAsync(element);
        }

        public List<CachedElement> GetAllCached()
        {
            return GetCache().GetAllCached();
        }

        public bool EditCached(int id, string value)
        {
            return GetCache().EditCached(id, value);
        }

        public CachedElement AddCachedChild(int parentId, string value)
        {
            return GetCache().AddCachedChild(parentId, value);
        }

        public bool DeleteCached(int id)
        {
            return GetCache().DeleteCached(id);
        }

        public Task ApplyAsync(AppDbContext db)
        {
            return GetCache().ApplyAsync(db);
        }

        public void Clear()
        {
            GetCache().Clear();
        }
    }
}
