using it.gis_landslide_detection.web.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Services
{
    public interface IHikingPointsService
    {
        Task<IEnumerable<HikingPointDto>> GetAllPointsAsync();
        Task<HikingPointDto?> GetPointByIdAsync(long id);
        Task<HikingPointDto> CreatePointAsync(HikingPointUpsertDto pointDto);
        Task<bool> UpdatePointAsync(long id, HikingPointUpsertDto pointDto);
        Task<bool> DeletePointAsync(long id);
    }
}
