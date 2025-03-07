using Microsoft.EntityFrameworkCore;

public class IndexerContext : DbContext
{
    public IndexerContext(DbContextOptions<IndexerContext> options)
        : base(options)
    {
    }

    // Tables
    public DbSet<Words> Words { get; set; }
    public DbSet<FileRecord> Files { get; set; }
    public DbSet<Occurrence> Occurrences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Map classes to tables in the database
        modelBuilder.Entity<Words>().ToTable("Words");
        modelBuilder.Entity<FileRecord>().ToTable("Files");
        modelBuilder.Entity<Occurrence>().ToTable("Occurrences");

        // Primary key for Occurrence
        modelBuilder.Entity<Occurrence>()
            .HasKey(o => new { o.WordId, o.FileId });

        // Relationship: Occurrence -> Words
        modelBuilder.Entity<Occurrence>()
            .HasOne(o => o.Word)
            .WithMany(w => w.Occurrences)
            .HasForeignKey(o => o.WordId);

        // Relationship: Occurrence -> FileRecord
        modelBuilder.Entity<Occurrence>()
            .HasOne(o => o.File)
            .WithMany(f => f.Occurrences)
            .HasForeignKey(o => o.FileId);

        // Make WordValue unique
        modelBuilder.Entity<Words>()
            .HasIndex(w => w.Word)
            .IsUnique();
    }
}
