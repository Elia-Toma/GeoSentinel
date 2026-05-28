using it.gis_landslide_detection.web.Models;
using Microsoft.EntityFrameworkCore;

namespace it.gis_landslide_detection.web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        { }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            
            //modelBuilder.Entity<HikingPoint>().Property(h => h.Geom)
            //    .HasColumnType("geometry");
            modelBuilder.Entity<HikingPoint>().ToTable("hiking_points");
            //base.OnModelCreating(modelBuilder);
            //modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<HikingPoint>()
                .Property(h => h.Geom)
                .HasColumnType("geometry");

            modelBuilder.Entity<IffiZone>()
                .Property(z => z.Geom)
                .HasColumnType("geometry");

            modelBuilder.Entity<HikingTrail>()
                .Property(t => t.Geom)
                .HasColumnType("geometry");

            modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<GisPoint>()
                .Property(p => p.Geom)
                .HasColumnType("geometry");

            modelBuilder.Entity<GisLine>()
                .Property(l => l.Geom)
                .HasColumnType("geometry");

            modelBuilder.Entity<GisPolygon>()
                .Property(p => p.Geom)
                .HasColumnType("geometry");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        public DbSet<HikingPoint> HikingPoints { get; set; }

        public DbSet<IffiZone> IffiZones { get; set; }

        public DbSet<HikingTrail> HikingTrails { get; set; }

        public DbSet<GisPoint> GisPoints { get; set; }
        public DbSet<GisLine> GisLines { get; set; }
        public DbSet<GisPolygon> GisPolygons { get; set; }
    }
}
