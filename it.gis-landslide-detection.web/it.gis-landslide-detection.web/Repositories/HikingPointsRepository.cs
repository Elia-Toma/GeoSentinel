using it.gis_landslide_detection.web.Data;
using it.gis_landslide_detection.web.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Repositories
{
    public class HikingPointsRepository : IHikingPointsRepository
    {
        private readonly ApplicationDbContext _context;

        public HikingPointsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<HikingPoint>> GetAllAsync()
        {
            return await _context.HikingPoints.ToListAsync();
        }

        public async Task<HikingPoint?> GetByIdAsync(long id)
        {
            return await _context.HikingPoints.FindAsync(id);
        }

        public async Task AddAsync(HikingPoint point)
        {
            await _context.HikingPoints.AddAsync(point);
        }

        public async Task UpdateAsync(HikingPoint point)
        {
            _context.HikingPoints.Update(point);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(long id)
        {
            var point = await _context.HikingPoints.FindAsync(id);
            if (point != null)
            {
                _context.HikingPoints.Remove(point);
            }
        }

        public async Task<bool> ExistsAsync(long id)
        {
            return await _context.HikingPoints.AnyAsync(p => p.Id == id);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
