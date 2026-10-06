using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace TreeEditor.Api.Services
{
    // Manages server-issued client keys stored in cache/allKeys.json
    public class KeyStore
    {
        private readonly string _filePath;
        private readonly object _lock = new object();
        private readonly HashSet<string> _keys = new();

        public KeyStore(IHostEnvironment env)
        {
            var basePath = Path.Combine(env.ContentRootPath ?? Directory.GetCurrentDirectory(), "cache");
            Directory.CreateDirectory(basePath);
            _filePath = Path.Combine(basePath, "allKeys.json");
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return;
                var json = File.ReadAllText(_filePath);
                var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                lock (_lock)
                {
                    _keys.Clear();
                    foreach (var k in list) _keys.Add(k);
                }
            }
            catch { }
        }

        private void Save()
        {
            try
            {
                lock (_lock)
                {
                    var json = JsonSerializer.Serialize(_keys.ToList());
                    File.WriteAllText(_filePath, json);
                }
            }
            catch { }
        }

        public string CreateKey()
        {
            var key = Guid.NewGuid().ToString();
            lock (_lock)
            {
                _keys.Add(key);
                Save();
            }
            return key;
        }

        public bool Validate(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (_lock)
            {
                return _keys.Contains(key);
            }
        }

        public void Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (_lock)
            {
                if (_keys.Remove(key)) Save();
            }
        }
    }
}
