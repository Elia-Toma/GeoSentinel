using it.gis_landslide_detection.web.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Repositories
{
    public interface ITrailsRepository
    {
        Task<IEnumerable<HikingTrail>> GetAllAsync();
        Task<HikingTrail?> GetByIdAsync(long id);
        Task AddAsync(HikingTrail trail);
        Task UpdateAsync(HikingTrail trail);
        Task DeleteAsync(long id);
        Task<bool> ExistsAsync(long id);
        Task SaveChangesAsync();
    }
}
