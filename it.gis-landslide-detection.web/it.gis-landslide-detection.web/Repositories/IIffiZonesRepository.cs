using it.gis_landslide_detection.web.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Repositories
{
    public interface IIffiZonesRepository
    {
        Task<IEnumerable<IffiZone>> GetAllAsync();
        Task<IffiZone?> GetByIdAsync(int id);
        Task AddAsync(IffiZone zone);
        Task UpdateAsync(IffiZone zone);
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
        Task SaveChangesAsync();
    }
}
