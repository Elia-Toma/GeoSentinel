using it.gis_landslide_detection.web.DTOs;
using it.gis_landslide_detection.web.Models;
using it.gis_landslide_detection.web.Repositories;
using NetTopologySuite.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Services
{
    public class HikingPointsService : IHikingPointsService
    {
        private readonly IHikingPointsRepository _repository;
        private readonly GeoJsonWriter _writer;
        private readonly GeoJsonReader _reader;

        public HikingPointsService(IHikingPointsRepository repository)
        {
            _repository = repository;
            _writer = new GeoJsonWriter();
            _reader = new GeoJsonReader();
        }

        public async Task<IEnumerable<HikingPointDto>> GetAllPointsAsync()
        {
            var points = await _repository.GetAllAsync();
            return points.Select(MapToDto);
        }

        public async Task<HikingPointDto?> GetPointByIdAsync(long id)
        {
            var point = await _repository.GetByIdAsync(id);
            return point != null ? MapToDto(point) : null;
        }

        public async Task<HikingPointDto> CreatePointAsync(HikingPointUpsertDto pointDto)
        {
            var point = new HikingPoint
            {
                Name = pointDto.Name,
                Type = pointDto.Type,
                Geom = pointDto.GeoJson != null ? _reader.Read<NetTopologySuite.Geometries.Geometry>(pointDto.GeoJson) : null
            };

            await _repository.AddAsync(point);
            await _repository.SaveChangesAsync();

            return MapToDto(point);
        }

        public async Task<bool> UpdatePointAsync(long id, HikingPointUpsertDto pointDto)
        {
            var point = await _repository.GetByIdAsync(id);
            if (point == null) return false;

            point.Name = pointDto.Name;
            point.Type = pointDto.Type;
            
            if (pointDto.GeoJson != null)
            {
                point.Geom = _reader.Read<NetTopologySuite.Geometries.Geometry>(pointDto.GeoJson);
            }

            await _repository.UpdateAsync(point);
            await _repository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePointAsync(long id)
        {
            if (!await _repository.ExistsAsync(id)) return false;

            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();
            return true;
        }

        private HikingPointDto MapToDto(HikingPoint point)
        {
            return new HikingPointDto
            {
                Id = point.Id,
                Name = point.Name,
                Type = point.Type,
                GeoJson = point.Geom != null ? _writer.Write(point.Geom) : null
            };
        }
    }
}
