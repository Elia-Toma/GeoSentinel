using it.gis_landslide_detection.web.Models;

namespace it.gis_landslide_detection.web.Services
{
    public interface IIffiService
    {
        /// <summary>
        /// Restituisce la zona IFFI in cui cade il punto (lat, lng),
        /// oppure null se il punto non interseca nessuna zona.
        /// </summary>
        Task<IffiZone?> GetZoneAsync(double lat, double lng);
    }
}
