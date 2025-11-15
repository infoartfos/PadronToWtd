using Microsoft.EntityFrameworkCore;
using PadronWtd.Domain.Entities;

namespace PadronWtd.Infrastructure.Persistence;

public class PadronDbContext : DbContext
{
    public PadronDbContext(DbContextOptions<PadronDbContext> options)
        : base(options)
    {
    }

    public DbSet<PadronEntry> PadronEntries => Set<PadronEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PadronEntry>(entity =>
        {
            entity.ToTable("PadronEntries");

            // No tiene ID propio, así que usamos una clave compuesta
            entity.HasKey(e => new { e.RunId, e.LineNumber });

            entity.Property(e => e.CUIT)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Denominacion)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.ActividadEconomica)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.NivelRiesgo)
                .HasMaxLength(50)
                .IsRequired();
        });
    }
}
