using TreeEditor.Domain;
using TreeEditor.Domain.Models;

namespace TreeEditor.Api.Services
{
    public interface IElementCache
    {
        Task<bool> LoadToCacheAsync(Element element);
        List<CachedElement> GetAllCached();
        bool EditCached(int id, string value);
        CachedElement AddCachedChild(int parentId, string value);
        bool DeleteCached(int id);
        Task ApplyAsync(AppDbContext db);
        void Clear();
    }

    public class CachedElement
    {
        public int Id { get; set; } // negative = new in cache, positive = existing DB id
        public int? ParentId { get; set; }
        public string Value { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
    }
}
