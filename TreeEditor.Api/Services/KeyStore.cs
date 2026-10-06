using System.Text.Json;

namespace TreeEditor.Api.Services
{
    // Manages server-issued client keys stored in cache/allKeys.json
    public class KeyStore
    {
        private readonly string _filePath;
        private readonly object _lock = new object();
        private readonly Dictionary<string, DateTime> _keys = new();

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
                var list = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json) ?? new Dictionary<string, DateTime>();
                lock (_lock)
                {
                    _keys.Clear();
                    foreach (var kvp in list) _keys.Add(kvp.Key, kvp.Value);
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
                    var json = JsonSerializer.Serialize(_keys);
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
                _keys.Add(key, DateTime.UtcNow.AddMinutes(15));
                // append-only write to avoid rewriting the whole file every time
                try
                {
                    Dictionary<string, DateTime> list = new();
                    if (File.Exists(_filePath))
                    {
                        var existing = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(_filePath)) ?? new Dictionary<string, DateTime>();
                        foreach (var kvp in existing)
                        {
                            if (!_keys.ContainsKey(kvp.Key))
                            {
                                list.Add(kvp.Key, kvp.Value);
                            }
                        }
                    }
                    list.Add(key, DateTime.UtcNow.AddMinutes(15));
                    File.WriteAllText(_filePath, JsonSerializer.Serialize(list));
                }
                catch { }
            }
            return key;
        }

        public bool Validate(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (_lock)
            {
                return _keys.ContainsKey(key) && _keys[key] > DateTime.UtcNow;
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

        public void Refresh(string key)
        {
            if (Validate(key))
            {
                lock (_lock)
                {
                    _keys[key] = DateTime.UtcNow.AddMinutes(15);
                    Save();
                }
            }
        }
    }

}
