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

        private ElementCache ResolveCache()
        {
            var ctx = _ctxAccessor.HttpContext;
            if (ctx == null) throw new InvalidOperationException("No HttpContext available");

            var id = "anonymous";
            if (ctx.Request.Headers.TryGetValue("X-Client-Id", out var hdr) && !string.IsNullOrEmpty(hdr))
            {
                id = hdr.ToString();
            }

            _logger?.LogDebug("PerUserElementCache.ResolveCache: X-Client-Id='{ClientId}' Path='{Path}'", id, ctx.Request.Path);

            return _store.GetOrCreate(id);
        }

        public Task<bool> LoadToCacheAsync(Element element) => ResolveCache().LoadToCacheAsync(element);

        public List<CachedElement> GetAllCached() => ResolveCache().GetAllCached();

        public bool EditCached(int id, string value) => ResolveCache().EditCached(id, value);

        public CachedElement AddCachedChild(int parentId, string value) => ResolveCache().AddCachedChild(parentId, value);

        public bool DeleteCached(int id) => ResolveCache().DeleteCached(id);

        public Task ApplyAsync(AppDbContext db) => ResolveCache().ApplyAsync(db);

        public void Clear() => ResolveCache().Clear();
    }
}
