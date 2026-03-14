using Microsoft.EntityFrameworkCore;

namespace it.gis_landslide_detection.web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        { }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        //public DbSet<GISCourse2025.Models.municipalities> municipalities { get; set; }

    }
}
