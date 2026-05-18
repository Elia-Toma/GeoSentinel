using it.gis_landslide_detection.web.DTOs;
using it.gis_landslide_detection.web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HikingPointsController : ControllerBase
    {
        private readonly IHikingPointsService _pointsService;

        public HikingPointsController(IHikingPointsService pointsService)
        {
            _pointsService = pointsService;
        }

        // GET /api/hikingpoints
        [HttpGet]
        public async Task<ActionResult<IEnumerable<HikingPointDto>>> GetAll()
        {
            var points = await _pointsService.GetAllPointsAsync();
            return Ok(points);
        }

        // GET /api/hikingpoints/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<HikingPointDto>> GetById(long id)
        {
            var point = await _pointsService.GetPointByIdAsync(id);
            if (point == null) return NotFound();
            return Ok(point);
        }

        // POST /api/hikingpoints
        [HttpPost]
        public async Task<ActionResult<HikingPointDto>> Create([FromBody] HikingPointUpsertDto pointDto)
        {
            if (pointDto == null) return BadRequest();

            var createdPoint = await _pointsService.CreatePointAsync(pointDto);
            return CreatedAtAction(nameof(GetById), new { id = createdPoint.Id }, createdPoint);
        }

        // PUT /api/hikingpoints/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] HikingPointUpsertDto pointDto)
        {
            if (pointDto == null) return BadRequest();

            var success = await _pointsService.UpdatePointAsync(id, pointDto);
            if (!success) return NotFound();

            return NoContent();
        }

        // DELETE /api/hikingpoints/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var success = await _pointsService.DeletePointAsync(id);
            if (!success) return NotFound();

            return NoContent();
        }
    }
}
