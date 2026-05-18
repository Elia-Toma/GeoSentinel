using it.gis_landslide_detection.web.DTOs;
using it.gis_landslide_detection.web.Models;
using it.gis_landslide_detection.web.Repositories;
using NetTopologySuite.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Services
{
    public class IffiZonesService : IIffiZonesService
    {
        private readonly IIffiZonesRepository _repository;
        private readonly GeoJsonWriter _writer;
        private readonly GeoJsonReader _reader;

        public IffiZonesService(IIffiZonesRepository repository)
        {
            _repository = repository;
            _writer = new GeoJsonWriter();
            _reader = new GeoJsonReader();
        }

        public async Task<IEnumerable<IffiZoneDto>> GetAllZonesAsync()
        {
            var zones = await _repository.GetAllAsync();
            return zones.Select(MapToDto);
        }

        public async Task<IffiZoneDto?> GetZoneByIdAsync(int id)
        {
            var zone = await _repository.GetByIdAsync(id);
            return zone != null ? MapToDto(zone) : null;
        }

        public async Task<IffiZoneDto> CreateZoneAsync(IffiZoneUpsertDto zoneDto)
        {
            var zone = new IffiZone
            {
                IdFrana = zoneDto.IdFrana,
                NomeTipo = zoneDto.NomeTipo,
                Geom = zoneDto.GeoJson != null ? _reader.Read<NetTopologySuite.Geometries.Geometry>(zoneDto.GeoJson) : null
            };

            await _repository.AddAsync(zone);
            await _repository.SaveChangesAsync();

            return MapToDto(zone);
        }

        public async Task<bool> UpdateZoneAsync(int id, IffiZoneUpsertDto zoneDto)
        {
            var zone = await _repository.GetByIdAsync(id);
            if (zone == null) return false;

            zone.IdFrana = zoneDto.IdFrana;
            zone.NomeTipo = zoneDto.NomeTipo;
            
            if (zoneDto.GeoJson != null)
            {
                zone.Geom = _reader.Read<NetTopologySuite.Geometries.Geometry>(zoneDto.GeoJson);
            }

            await _repository.UpdateAsync(zone);
            await _repository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteZoneAsync(int id)
        {
            if (!await _repository.ExistsAsync(id)) return false;

            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();
            return true;
        }

        private IffiZoneDto MapToDto(IffiZone zone)
        {
            return new IffiZoneDto
            {
                Id = zone.Id,
                IdFrana = zone.IdFrana,
                NomeTipo = zone.NomeTipo,
                GeoJson = zone.Geom != null ? _writer.Write(zone.Geom) : null
            };
        }
    }
}
