using it.gis_landslide_detection.web.Data;
using it.gis_landslide_detection.web.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace it.gis_landslide_detection.web.Services
{
    public class IffiService : IIffiService
    {
        private readonly ApplicationDbContext _context;

        public IffiService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IffiZone?> GetZoneAsync(double lat, double lng)
        {
            // Creazione del punto geografico con SRID 4326 (WGS84)
            var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

            // ATTENZIONE: PostGIS usa (x=lng, y=lat)
            var punto = factory.CreatePoint(new Coordinate(lng, lat));

            // Query spaziale ST_Within
            return await _context.IffiZones
                .Where(z => z.Geom != null && z.Geom.Contains(punto))
                .FirstOrDefaultAsync();
        }
    }
}