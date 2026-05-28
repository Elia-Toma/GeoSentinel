using it.gis_landslide_detection.web.Data;
using it.gis_landslide_detection.web.Helpers;
using it.gis_landslide_detection.web.Models;
using it.gis_landslide_detection.web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GisDataController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IRoutingService _routingService;
        private readonly GeometryFactory _geometryFactory;

        public GisDataController(ApplicationDbContext context, IRoutingService routingService)
        {
            _context = context;
            _routingService = routingService;
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        }

        // ==========================================
        // 1. READ / QUERY ENDPOINTS (GEOJSON OUTPUT)
        // ==========================================

        // GET: api/GisData/points
        [HttpGet("points")]
        public async Task<IActionResult> GetPoints([FromQuery] string? type)
        {
            var query = _context.GisPoints.AsQueryable();
            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(p => p.Type == type);
            }

            var points = await query.ToListAsync();
            var features = GeoJsonFormatter.ToFeatureCollection(points, 
                p => p.Geom, 
                p => new Dictionary<string, object> { { "id", p.Id }, { "name", p.Name ?? "" }, { "type", p.Type ?? "" } });

            var geoJson = GeoJsonFormatter.Format(features);
            return Content(geoJson, "application/json");
        }

        // GET: api/GisData/lines
        [HttpGet("lines")]
        public async Task<IActionResult> GetLines([FromQuery] string? type)
        {
            var query = _context.GisLines.AsQueryable();
            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(l => l.Type == type);
            }

            var lines = await query.ToListAsync();
            var features = GeoJsonFormatter.ToFeatureCollection(lines, 
                l => l.Geom, 
                l => new Dictionary<string, object> { { "id", l.Id }, { "name", l.Name ?? "" }, { "type", l.Type ?? "" } });

            var geoJson = GeoJsonFormatter.Format(features);
            return Content(geoJson, "application/json");
        }

        // GET: api/GisData/polygons
        [HttpGet("polygons")]
        public async Task<IActionResult> GetPolygons([FromQuery] int? minPopulation)
        {
            var query = _context.GisPolygons.AsQueryable();
            if (minPopulation.HasValue)
            {
                query = query.Where(p => p.Population >= minPopulation.Value);
            }

            var polygons = await query.ToListAsync();
            var features = GeoJsonFormatter.ToFeatureCollection(polygons, 
                p => p.Geom, 
                p => new Dictionary<string, object> { { "id", p.Id }, { "name", p.Name ?? "" }, { "population", p.Population } });

            var geoJson = GeoJsonFormatter.Format(features);
            return Content(geoJson, "application/json");
        }

        // GET: api/GisData/nearest
        [HttpGet("nearest")]
        public async Task<IActionResult> GetNearestPoints([FromQuery] double lat, [FromQuery] double lng, [FromQuery] int limit = 5)
        {
            var location = _geometryFactory.CreatePoint(new Coordinate(lng, lat));

            var points = await _context.GisPoints
                .OrderBy(p => p.Geom!.Distance(location))
                .Take(limit)
                .ToListAsync();

            var features = GeoJsonFormatter.ToFeatureCollection(points, 
                p => p.Geom, 
                p => new Dictionary<string, object> { { "id", p.Id }, { "name", p.Name ?? "" }, { "type", p.Type ?? "" } });

            var geoJson = GeoJsonFormatter.Format(features);
            return Content(geoJson, "application/json");
        }

        // POST: api/GisData/points/within (Spatial query)
        [HttpPost("points/within")]
        public async Task<IActionResult> GetPointsWithin([FromBody] PolygonDto areaDto)
        {
            if (areaDto.Coordinates == null || areaDto.Coordinates.Count < 3)
            {
                return BadRequest("Geometria del poligono non valida.");
            }

            var shell = areaDto.Coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray();
            var searchPolygon = _geometryFactory.CreatePolygon(shell);

            var pointsQuery = _context.GisPoints.Where(p => p.Geom!.Within(searchPolygon));
            if (!string.IsNullOrEmpty(areaDto.Type) && areaDto.Type != "Mostra tutto")
            {
                pointsQuery = pointsQuery.Where(p => p.Type == areaDto.Type);
            }
            var points = await pointsQuery.ToListAsync();

            var features = GeoJsonFormatter.ToFeatureCollection(points, 
                p => p.Geom, 
                p => new Dictionary<string, object> { { "id", p.Id }, { "name", p.Name ?? "" }, { "type", p.Type ?? "" } });

            var geoJson = GeoJsonFormatter.Format(features);
            return Content(geoJson, "application/json");
        }

        // GET: api/GisData/route (Dijkstra Routing)
        [HttpGet("route")]
        public async Task<IActionResult> GetRoute([FromQuery] double startLat, [FromQuery] double startLng, [FromQuery] double endLat, [FromQuery] double endLng)
        {
            var start = new Coordinate(startLng, startLat);
            var end = new Coordinate(endLng, endLat);

            var result = await _routingService.CalculateShortestPathAsync(start, end);
            if (result == null || result.Path == null)
            {
                return NotFound("Nessun percorso trovato tra i punti selezionati.");
            }

            var feature = GeoJsonFormatter.ToFeature(result.Path, 
                g => g, 
                g => new Dictionary<string, object> 
                { 
                    { "name", "Shortest Path" }, 
                    { "length_deg", result.Path.Length },
                    { "snappedStartLat", result.SnappedStart!.Y },
                    { "snappedStartLng", result.SnappedStart!.X },
                    { "snappedEndLat", result.SnappedEnd!.Y },
                    { "snappedEndLng", result.SnappedEnd!.X }
                });

            if (feature == null)
            {
                return NotFound("Errore durante la creazione della feature geografica.");
            }

            var geoJson = GeoJsonFormatter.Format(feature);
            return Content(geoJson, "application/json");
        }

        // ==========================================
        // 2. CRUD OPERATIONS (WRITE/EDIT/DELETE)
        // ==========================================

        // POST: api/GisData/points
        [HttpPost("points")]
        public async Task<ActionResult> CreatePoint([FromBody] PointDto dto)
        {
            var point = new GisPoint
            {
                Name = dto.Name,
                Type = dto.Type,
                Geom = _geometryFactory.CreatePoint(new Coordinate(dto.Lng, dto.Lat))
            };

            _context.GisPoints.Add(point);
            await _context.SaveChangesAsync();

            return Ok(new { id = point.Id, message = "Punto aggiunto correttamente!" });
        }

        // PUT: api/GisData/points/{id} (Editing della posizione o dei metadati)
        [HttpPut("points/{id}")]
        public async Task<ActionResult> UpdatePoint(long id, [FromBody] PointDto dto)
        {
            var point = await _context.GisPoints.FindAsync(id);
            if (point == null) return NotFound();

            point.Name = dto.Name;
            point.Type = dto.Type;
            point.Geom = _geometryFactory.CreatePoint(new Coordinate(dto.Lng, dto.Lat));

            await _context.SaveChangesAsync();
            return Ok(new { message = "Punto aggiornato correttamente!" });
        }

        // DELETE: api/GisData/points/{id}
        [HttpDelete("points/{id}")]
        public async Task<ActionResult> DeletePoint(long id)
        {
            var point = await _context.GisPoints.FindAsync(id);
            if (point == null) return NotFound();

            _context.GisPoints.Remove(point);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Punto eliminato correttamente!" });
        }

        // POST: api/GisData/lines
        [HttpPost("lines")]
        public async Task<ActionResult> CreateLine([FromBody] LineDto dto)
        {
            if (dto.Coordinates == null || dto.Coordinates.Count < 2)
            {
                return BadRequest("Una linea deve contenere almeno due punti.");
            }

            var coords = dto.Coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray();
            var line = new GisLine
            {
                Name = dto.Name,
                Type = dto.Type,
                Geom = _geometryFactory.CreateLineString(coords)
            };

            _context.GisLines.Add(line);
            await _context.SaveChangesAsync();

            if (line.Type == "Road")
            {
                try { await _context.Database.ExecuteSqlRawAsync("SELECT refresh_routing_topology();"); } catch { }
            }

            return Ok(new { id = line.Id, message = "Linea creata correttamente!" });
        }

        // PUT: api/GisData/lines/{id} (Editing dei vertici della linea)
        [HttpPut("lines/{id}")]
        public async Task<ActionResult> UpdateLine(long id, [FromBody] LineDto dto)
        {
            var line = await _context.GisLines.FindAsync(id);
            if (line == null) return NotFound();

            if (dto.Coordinates == null || dto.Coordinates.Count < 2)
            {
                return BadRequest("Una linea deve contenere almeno due punti.");
            }

            var coords = dto.Coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray();
            line.Name = dto.Name;
            line.Type = dto.Type;
            line.Geom = _geometryFactory.CreateLineString(coords);

            await _context.SaveChangesAsync();
            
            if (line.Type == "Road" || dto.Type == "Road")
            {
                try { await _context.Database.ExecuteSqlRawAsync("SELECT refresh_routing_topology();"); } catch { }
            }
            
            return Ok(new { message = "Linea aggiornata correttamente!" });
        }

        // DELETE: api/GisData/lines/{id}
        [HttpDelete("lines/{id}")]
        public async Task<ActionResult> DeleteLine(long id)
        {
            var line = await _context.GisLines.FindAsync(id);
            if (line == null) return NotFound();

            _context.GisLines.Remove(line);
            await _context.SaveChangesAsync();

            if (line.Type == "Road")
            {
                try { await _context.Database.ExecuteSqlRawAsync("SELECT refresh_routing_topology();"); } catch { }
            }

            return Ok(new { message = "Linea eliminata correttamente!" });
        }

        // POST: api/GisData/polygons
        [HttpPost("polygons")]
        public async Task<ActionResult> CreatePolygon([FromBody] PolygonDto dto)
        {
            if (dto.Coordinates == null || dto.Coordinates.Count < 3)
            {
                return BadRequest("Un poligono deve contenere almeno tre punti.");
            }

            var coords = dto.Coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray();
            var polygon = new GisPolygon
            {
                Name = dto.Name,
                Population = dto.Population,
                Geom = _geometryFactory.CreatePolygon(coords)
            };

            _context.GisPolygons.Add(polygon);
            await _context.SaveChangesAsync();

            return Ok(new { id = polygon.Id, message = "Poligono creato correttamente!" });
        }

        // PUT: api/GisData/polygons/{id} (Editing della forma del poligono)
        [HttpPut("polygons/{id}")]
        public async Task<ActionResult> UpdatePolygon(long id, [FromBody] PolygonDto dto)
        {
            var polygon = await _context.GisPolygons.FindAsync(id);
            if (polygon == null) return NotFound();

            if (dto.Coordinates == null || dto.Coordinates.Count < 3)
            {
                return BadRequest("Un poligono deve contenere almeno tre punti.");
            }

            var coords = dto.Coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray();
            polygon.Name = dto.Name;
            polygon.Population = dto.Population;
            polygon.Geom = _geometryFactory.CreatePolygon(coords);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Poligono aggiornato correttamente!" });
        }

        // DELETE: api/GisData/polygons/{id}
        [HttpDelete("polygons/{id}")]
        public async Task<ActionResult> DeletePolygon(long id)
        {
            var polygon = await _context.GisPolygons.FindAsync(id);
            if (polygon == null) return NotFound();

            _context.GisPolygons.Remove(polygon);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Poligono eliminato correttamente!" });
        }
    }

    // ==========================================
    // 3. DTO DATA TRANSFER OBJECTS FOR API
    // ==========================================

    public class PointDto
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    public class LineDto
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public List<double[]> Coordinates { get; set; } = new(); // [[lng, lat], [lng, lat]]
    }

    public class PolygonDto
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public int Population { get; set; }
        public List<double[]> Coordinates { get; set; } = new(); // [[lng, lat], [lng, lat], ... , [lng, lat]] (il primo e l'ultimo elemento devono coincidere)
    }
}
