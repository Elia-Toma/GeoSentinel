// Controllers/TrailsController.cs
using it.gis_landslide_detection.web.Data;
using it.gis_landslide_detection.web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;
using System;
using System.Linq;
using System.Threading;
using it.gis_landslide_detection.web.Models;
using it.gis_landslide_detection.web.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace it.gis_landslide_detection.web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrailsController : ControllerBase
    {
        private readonly ITrailsService _trailsService;
        private readonly IIffiService _iffi;
        private readonly ISentinelService _sentinel;
        private readonly IWeatherService _weather;
        private readonly IHazardScoreEngine _hazardEngine;
        private readonly ILogger<TrailsController> _logger;
        private readonly IMemoryCache _cache;
        private static readonly SemaphoreSlim[] _shardedLocks = Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

        public TrailsController(
            ITrailsService trailsService, 
            IIffiService iffiService, 
            ISentinelService sentinelService, 
            IWeatherService weatherService, 
            IHazardScoreEngine hazardEngine, 
            ILogger<TrailsController> logger, 
            IMemoryCache cache)
        {
            _trailsService = trailsService;
            _iffi = iffiService;
            _sentinel = sentinelService;
            _weather = weatherService;
            _hazardEngine = hazardEngine;
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// Sanitizes a double value: replaces NaN and Infinity with a fallback (default 0).
        /// This prevents System.Text.Json serialization failures.
        /// </summary>
        private static double Safe(double value, double fallback = 0.0)
            => double.IsFinite(value) ? value : fallback;

        // GET /api/trails
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TrailDto>>> GetAll()
        {
            var trails = await _trailsService.GetAllTrailsAsync();
            return Ok(trails);
        }
        
        // GET /api/trails/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<TrailDto>> GetById(long id)
        {
            var trail = await _trailsService.GetTrailByIdAsync(id);
            if (trail == null) return NotFound();
            return Ok(trail);
        }

        // POST /api/trails
        [HttpPost]
        public async Task<ActionResult<TrailDto>> Create([FromBody] TrailUpsertDto trailDto)
        {
            if (trailDto == null) return BadRequest();
            
            var createdTrail = await _trailsService.CreateTrailAsync(trailDto);
            return CreatedAtAction(nameof(GetById), new { id = createdTrail.Id }, createdTrail);
        }
        
        // PUT /api/trails/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] TrailUpsertDto trailDto)
        {
            if (trailDto == null) return BadRequest();

            var success = await _trailsService.UpdateTrailAsync(id, trailDto);
            if (!success) return NotFound();

            return NoContent();
        }

        // DELETE /api/trails/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var success = await _trailsService.DeleteTrailAsync(id);
            if (!success) return NotFound();

            return NoContent();
        }

        // GET /api/trails/{id}/hazard
        [HttpGet("{id}/hazard")]
        public async Task<IActionResult> GetHazard(long id)
        {
          string cacheKey = $"trail_hazard_{id}";
          if (_cache.TryGetValue(cacheKey, out object? cachedResponse))
          {
              return Ok(cachedResponse);
          }

          var semaphore = _shardedLocks[Math.Abs(cacheKey.GetHashCode()) % 256];
          await semaphore.WaitAsync();

          try
          {
            if (_cache.TryGetValue(cacheKey, out cachedResponse))
            {
                return Ok(cachedResponse);
            }

            // get punto critico lungo il trail
            var iffiResult = await _iffi.GetTrailHazardAsync(id);
            if (iffiResult == null)
                return NotFound(new { error = $"Trail {id} non trovato." });

            // Usa le coordinate del punto critico o del trail calcolate dal hazard calculator per gli altri service
            double queryLat = iffiResult.ReferenceLat;
            double queryLng = iffiResult.ReferenceLng;

            // Chiama Sentinel e Weather per il punto critico
            var sentinel = await _sentinel
                .GetSoilMoistureForPointAsync(queryLat, queryLng);
            var weather = await _weather
                .GetCurrentPrecipitationAsync(queryLat, queryLng);

            // Valori con fallback
            bool sentinelUnavailable = sentinel == null;
            int soilScore = sentinel?.SoilMoistureScore ?? 0;
            double vvDb = sentinel?.VvMeanDb ?? -20.0; // Default a secco invece di 0 (che per il SAR significa saturo)
            string sentinelSrc = sentinel?.Fonte ?? "Dati non disponibili";
            
            bool weatherDataUnavailable = weather == null;

            int apiScore = weather?.ApiScore ?? 0;
            double apiMm = weather?.AntecedentPrecipIndex ?? 0;
            int currentRainScore = weather?.CurrentRainScore ?? 0;
            double precipMmh = weather?.PrecipitationMmh ?? 0;
            string meteoSrc = weather?.Source ?? "fallback";

            // --- Calcolo pericolosità tramite HazardScoreEngine (R1/R2/R3 inclusi) ---
            double histScore = iffiResult.HazardScore;
            var hazard = _hazardEngine.Calculate(
                iffiHazardScore:  histScore,
                iffiTipo:         iffiResult.IffiTipo,
                soilMoistureScore: soilScore,
                apiScore:         apiScore,
                currentRainScore: currentRainScore,
                precipMmh:        precipMmh,
                weatherDataUnavailable: weatherDataUnavailable
            );

            double hazardScore = Safe(hazard.HazardScore);
            histScore = Safe(histScore);

            var responseObj = new
            {
                // Trail
                TrailId = id,
                TrailName = iffiResult.TrailName,
                // Score finale
                HazardScore = (int)hazardScore,
                HazardLevel = hazard.HazardLevel,
                Message = hazard.HazardLevel switch
                {
                    "CRITICAL" => "Sentiero bloccato: pericolosità frana critica.",
                    "HIGH"     => "Sconsigliato: elevata probabilità di instabilità.",
                    "MEDIUM"   => "Percorrere con cautela.",
                    _          => "Sentiero sicuro."
                },
                // Punto critico da mostrare sulla mappa
                CriticalPointLat = iffiResult.HasHazard ? Safe(queryLat) : (double?)null,
                CriticalPointLng = iffiResult.HasHazard ? Safe(queryLng) : (double?)null,
                
                // Componenti diagnostiche
                Components = new 
                {
                    Iffi = new { Score = (int)Safe(histScore), Weight = 0.35, Tipo = iffiResult.IffiTipo, ZoneCount = iffiResult.ZoneCount },
                    SoilMoisture = new { 
                        Unavailable = sentinelUnavailable, 
                        Score = soilScore, 
                        Weight = Safe(Math.Round(hazard.WSoil, 4)), 
                        VvDb = Safe(Math.Round(vvDb, 2)), 
                        Source = sentinelSrc 
                    },
                    AntecedentPrecip = new { 
                        Score = apiScore, 
                        Weight = Safe(Math.Round(hazard.WApi, 4)), 
                        ApiMm = Safe(Math.Round(apiMm, 2)), 
                        Days = 7, 
                        DecayK = 0.85,
                        DailyHistory = weather?.DailyHistory 
                    },
                    CurrentRain = new { Score = currentRainScore, Weight = Safe(Math.Round(hazard.WRain, 4)), Mmh = Safe(Math.Round(precipMmh, 2)), Source = meteoSrc },
                    // Diagnostica formula
                    SaturationIndex = Safe(Math.Round(hazard.SaturationIndex, 2)),
                    TriggerMultiplier = Safe(Math.Round(hazard.TriggerMultiplier, 4)),
                    BaseHazard = Safe(hazard.BaseHazard),
                    FlashOverrideApplied = hazard.FlashOverrideApplied,
                    SaturationFloorApplied = hazard.SaturationFloorApplied,
                    WeatherDataUnavailable = hazard.WeatherDataUnavailable
                }
            };

            _cache.Set(cacheKey, responseObj, TimeSpan.FromMinutes(15));
            return Ok(responseObj);
          }
          catch (Exception ex)
          {
              _logger.LogError(ex, "Errore nel calcolo della pericolosità per trail {TrailId}", id);
              return StatusCode(500, new { error = $"Errore interno nel calcolo della pericolosità per il trail {id}.", detail = ex.Message });
          }
          finally
          {
              semaphore.Release();
          }
        }

    }
}