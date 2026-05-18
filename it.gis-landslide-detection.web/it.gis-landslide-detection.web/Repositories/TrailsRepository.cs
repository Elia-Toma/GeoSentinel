using it.gis_landslide_detection.web.Data;
using it.gis_landslide_detection.web.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Repositories
{
    public class TrailsRepository : ITrailsRepository
    {
        private readonly ApplicationDbContext _context;

        public TrailsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<HikingTrail>> GetAllAsync()
        {
            return await _context.HikingTrails.ToListAsync();
        }

        public async Task<HikingTrail?> GetByIdAsync(long id)
        {
            return await _context.HikingTrails.FindAsync(id);
        }

        public async Task AddAsync(HikingTrail trail)
        {
            await _context.HikingTrails.AddAsync(trail);
        }

        public async Task UpdateAsync(HikingTrail trail)
        {
            _context.HikingTrails.Update(trail);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(long id)
        {
            var trail = await _context.HikingTrails.FindAsync(id);
            if (trail != null)
            {
                _context.HikingTrails.Remove(trail);
            }
        }

        public async Task<bool> ExistsAsync(long id)
        {
            return await _context.HikingTrails.AnyAsync(t => t.Id == id);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
