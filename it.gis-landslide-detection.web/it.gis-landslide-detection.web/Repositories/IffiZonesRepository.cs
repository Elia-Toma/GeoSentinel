using it.gis_landslide_detection.web.Data;
using it.gis_landslide_detection.web.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Repositories
{
    public class IffiZonesRepository : IIffiZonesRepository
    {
        private readonly ApplicationDbContext _context;

        public IffiZonesRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<IffiZone>> GetAllAsync()
        {
            return await _context.IffiZones.ToListAsync();
        }

        public async Task<IffiZone?> GetByIdAsync(int id)
        {
            return await _context.IffiZones.FindAsync(id);
        }

        public async Task AddAsync(IffiZone zone)
        {
            await _context.IffiZones.AddAsync(zone);
        }

        public async Task UpdateAsync(IffiZone zone)
        {
            _context.IffiZones.Update(zone);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(int id)
        {
            var zone = await _context.IffiZones.FindAsync(id);
            if (zone != null)
            {
                _context.IffiZones.Remove(zone);
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.IffiZones.AnyAsync(z => z.Id == id);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
