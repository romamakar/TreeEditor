using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TreeEditor.Api.Services
{
    // Holds ElementCache instances per client id
    public class ElementCacheStore
    {
        private readonly ConcurrentDictionary<string, ElementCache> _store = new();
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _basePath;

        public ElementCacheStore(ILoggerFactory loggerFactory, IHostEnvironment env)
        {
            _loggerFactory = loggerFactory;
            _basePath = Path.Combine(env.ContentRootPath ?? Directory.GetCurrentDirectory(), "cache");
            Directory.CreateDirectory(_basePath);
        }

        public ElementCache GetOrCreate(string clientId)
        {
            // sanitize clientId to avoid path traversal or invalid file names
            var safe = (clientId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(safe)) safe = "anon";
            // allow only letters, digits, dash and underscore; replace others with '_'
            safe = Regex.Replace(safe, "[^A-Za-z0-9_-]", "_");
            // limit length
            if (safe.Length > 64) safe = safe.Substring(0, 64);

            return _store.GetOrAdd(safe, id =>
            {
                var persistPath = Path.Combine(_basePath, id + ".json");
                return new ElementCache(_loggerFactory.CreateLogger<ElementCache>(), persistPath);
            });
        }

        public bool TryRemove(string clientId)
        {
            return _store.TryRemove(clientId, out _);
        }

        public void ClearAll()
        {
            var values = _store.Values.ToList();
            foreach (var c in values)
            {
                try { c.Clear(); } catch { }
            }
            _store.Clear();
            // also remove persisted files
            try
            {
                if (Directory.Exists(_basePath))
                {
                    foreach (var f in Directory.GetFiles(_basePath, "*.json"))
                        File.Delete(f);
                }
            }
            catch { }
        }
    }
}
