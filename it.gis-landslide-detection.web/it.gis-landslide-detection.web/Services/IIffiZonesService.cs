using it.gis_landslide_detection.web.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Services
{
    public interface IIffiZonesService
    {
        Task<IEnumerable<IffiZoneDto>> GetAllZonesAsync();
        Task<IffiZoneDto?> GetZoneByIdAsync(int id);
        Task<IffiZoneDto> CreateZoneAsync(IffiZoneUpsertDto zoneDto);
        Task<bool> UpdateZoneAsync(int id, IffiZoneUpsertDto zoneDto);
        Task<bool> DeleteZoneAsync(int id);
    }
}
