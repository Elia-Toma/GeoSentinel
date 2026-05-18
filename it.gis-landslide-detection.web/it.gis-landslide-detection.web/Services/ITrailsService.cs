using it.gis_landslide_detection.web.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Services
{
    public interface ITrailsService
    {
        Task<IEnumerable<TrailDto>> GetAllTrailsAsync();
        Task<TrailDto?> GetTrailByIdAsync(long id);
        Task<TrailDto> CreateTrailAsync(TrailUpsertDto trailDto);
        Task<bool> UpdateTrailAsync(long id, TrailUpsertDto trailDto);
        Task<bool> DeleteTrailAsync(long id);
    }
}
