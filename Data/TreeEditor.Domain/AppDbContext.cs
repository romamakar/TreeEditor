using Microsoft.EntityFrameworkCore;
using TreeEditor.Domain.Models;

namespace TreeEditor.Domain
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Element> Elements { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Element>().HasKey(e => e.Id);
            modelBuilder.Entity<Element>()
                .HasOne<Element>()
                .WithMany()
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }

        public void Seed()
        {
            Database.EnsureCreated();
            if (!Elements.Any())
            {
                // Seed sample data with 4 levels
                var root = new Element { Value = "Root", ParentId = null };
                Elements.Add(root);
                SaveChanges();

                var childA = new Element { Value = "Child A", ParentId = root.Id };
                var childB = new Element { Value = "Child B", ParentId = root.Id };
                Elements.AddRange(childA, childB);
                SaveChanges();

                var a1 = new Element { Value = "Child A.1", ParentId = childA.Id };
                Elements.Add(a1);
                SaveChanges();

                var a1a = new Element { Value = "Child A.1.a", ParentId = a1.Id };
                Elements.Add(a1a);
                SaveChanges();
            }
        }
    }
}
