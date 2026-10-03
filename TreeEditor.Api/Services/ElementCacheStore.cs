using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TreeEditor.Api.Services
{
    // Holds ElementCache instances per client id
    public class ElementCacheStore
    {
        private readonly ConcurrentDictionary<string, ElementCache> _store = new();
        private readonly ILoggerFactory _loggerFactory;

        public ElementCacheStore(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public ElementCache GetOrCreate(string clientId)
        {
            return _store.GetOrAdd(clientId, _ => new ElementCache(_loggerFactory.CreateLogger<ElementCache>()));
        }

        public bool TryRemove(string clientId)
        {
            return _store.TryRemove(clientId, out _);
        }
    }
}
