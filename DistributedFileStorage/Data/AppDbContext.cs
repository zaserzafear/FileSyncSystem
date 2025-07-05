using DistributedFileStorage.Models;
using Microsoft.EntityFrameworkCore;

namespace DistributedFileStorage.Data;

public class AppDbContext : DbContext
{
    public DbSet<FileMetadata> Files { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileMetadata>()
            .HasIndex(f => new { f.FilePath, f.Revision })
            .IsUnique();
    }
}
