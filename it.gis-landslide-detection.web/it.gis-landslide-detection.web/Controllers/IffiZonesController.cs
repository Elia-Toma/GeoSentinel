using it.gis_landslide_detection.web.DTOs;
using it.gis_landslide_detection.web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace it.gis_landslide_detection.web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IffiZonesController : ControllerBase
    {
        private readonly IIffiZonesService _zonesService;

        public IffiZonesController(IIffiZonesService zonesService)
        {
            _zonesService = zonesService;
        }

        // GET /api/iffizones
        [HttpGet]
        public async Task<ActionResult<IEnumerable<IffiZoneDto>>> GetAll()
        {
            var zones = await _zonesService.GetAllZonesAsync();
            return Ok(zones);
        }

        // GET /api/iffizones/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<IffiZoneDto>> GetById(int id)
        {
            var zone = await _zonesService.GetZoneByIdAsync(id);
            if (zone == null) return NotFound();
            return Ok(zone);
        }

        // POST /api/iffizones
        [HttpPost]
        public async Task<ActionResult<IffiZoneDto>> Create([FromBody] IffiZoneUpsertDto zoneDto)
        {
            if (zoneDto == null) return BadRequest();

            var createdZone = await _zonesService.CreateZoneAsync(zoneDto);
            return CreatedAtAction(nameof(GetById), new { id = createdZone.Id }, createdZone);
        }

        // PUT /api/iffizones/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] IffiZoneUpsertDto zoneDto)
        {
            if (zoneDto == null) return BadRequest();

            var success = await _zonesService.UpdateZoneAsync(id, zoneDto);
            if (!success) return NotFound();

            return NoContent();
        }

        // DELETE /api/iffizones/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _zonesService.DeleteZoneAsync(id);
            if (!success) return NotFound();

            return NoContent();
        }
    }
}
