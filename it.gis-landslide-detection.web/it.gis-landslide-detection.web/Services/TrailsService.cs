using it.gis_landslide_detection.web.DTOs;
using it.gis_landslide_detection.web.Models;
using it.gis_landslide_detection.web.Repositories;
using NetTopologySuite.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Services
{
    public class TrailsService : ITrailsService
    {
        private readonly ITrailsRepository _repository;
        private readonly GeoJsonWriter _writer;
        private readonly GeoJsonReader _reader;

        public TrailsService(ITrailsRepository repository)
        {
            _repository = repository;
            _writer = new GeoJsonWriter();
            _reader = new GeoJsonReader();
        }

        public async Task<IEnumerable<TrailDto>> GetAllTrailsAsync()
        {
            var trails = await _repository.GetAllAsync();
            return trails.Select(MapToDto);
        }

        public async Task<TrailDto?> GetTrailByIdAsync(long id)
        {
            var trail = await _repository.GetByIdAsync(id);
            return trail != null ? MapToDto(trail) : null;
        }

        public async Task<TrailDto> CreateTrailAsync(TrailUpsertDto trailDto)
        {
            var trail = new HikingTrail
            {
                OsmId = trailDto.OsmId,
                Name = trailDto.Name,
                SacScale = trailDto.SacScale,
                Geom = trailDto.GeoJson != null ? _reader.Read<NetTopologySuite.Geometries.Geometry>(trailDto.GeoJson) : null
            };

            await _repository.AddAsync(trail);
            await _repository.SaveChangesAsync();

            return MapToDto(trail);
        }

        public async Task<bool> UpdateTrailAsync(long id, TrailUpsertDto trailDto)
        {
            var trail = await _repository.GetByIdAsync(id);
            if (trail == null) return false;

            trail.OsmId = trailDto.OsmId;
            trail.Name = trailDto.Name;
            trail.SacScale = trailDto.SacScale;
            
            if (trailDto.GeoJson != null)
            {
                trail.Geom = _reader.Read<NetTopologySuite.Geometries.Geometry>(trailDto.GeoJson);
            }

            await _repository.UpdateAsync(trail);
            await _repository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteTrailAsync(long id)
        {
            if (!await _repository.ExistsAsync(id)) return false;

            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();
            return true;
        }

        private TrailDto MapToDto(HikingTrail trail)
        {
            return new TrailDto
            {
                Id = trail.Id,
                OsmId = trail.OsmId,
                Name = trail.Name,
                SacScale = trail.SacScale,
                GeoJson = trail.Geom != null ? _writer.Write(trail.Geom) : null
            };
        }
    }
}
