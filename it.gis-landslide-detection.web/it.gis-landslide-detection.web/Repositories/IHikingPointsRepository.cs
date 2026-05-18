using it.gis_landslide_detection.web.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Repositories
{
    public interface IHikingPointsRepository
    {
        Task<IEnumerable<HikingPoint>> GetAllAsync();
        Task<HikingPoint?> GetByIdAsync(long id);
        Task AddAsync(HikingPoint point);
        Task UpdateAsync(HikingPoint point);
        Task DeleteAsync(long id);
        Task<bool> ExistsAsync(long id);
        Task SaveChangesAsync();
    }
}
