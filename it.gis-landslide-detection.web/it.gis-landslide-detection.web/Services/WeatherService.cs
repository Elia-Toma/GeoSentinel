using System;
using System.Collections.Generic;
using it.gis_landslide_detection.web.Models;
using System.Text.Json;
using System.Threading;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace it.gis_landslide_detection.web.Services
{
    public class WeatherService : IWeatherService
    {
        private readonly IHttpClientFactory _factory;
        private readonly ILogger<WeatherService> _log;
        private readonly IMemoryCache _cache;
        private static readonly SemaphoreSlim[] _shardedLocks = Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

        public WeatherService(IHttpClientFactory factory,
                              ILogger<WeatherService> log,
                              IMemoryCache cache)
        {
            _factory = factory;
            _log = log;
            _cache = cache;
        }

        public async Task<WeatherData?> GetCurrentPrecipitationAsync(
            double lat, double lng)
        {
            string cacheKey = $"weather:{lat:F2},{lng:F2}";
            if (_cache.TryGetValue(cacheKey, out WeatherData? cachedData))
            {
                _log.LogInformation("Returning Weather data from cache for cell {Lat:F2}, {Lng:F2}", lat, lng);
                return cachedData;
            }

            var semaphore = _shardedLocks[Math.Abs(cacheKey.GetHashCode()) % 256];
            await semaphore.WaitAsync();

            try
            {
                if (_cache.TryGetValue(cacheKey, out cachedData))
                    return cachedData;

                var client = _factory.CreateClient("openmeteo");
                
                // Richiede precipitazioni attuali e 7 giorni passati
                var url = System.FormattableString.Invariant($"/v1/forecast?latitude={lat:F4}&longitude={lng:F4}&current=precipitation&daily=precipitation_sum&past_days=7&forecast_days=1&timezone=auto&timeformat=unixtime");

                var res = await client.GetAsync(url);
                res.EnsureSuccessStatusCode();

                var json = await res.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                var mmh = doc.RootElement
                    .GetProperty("current")
                    .GetProperty("precipitation")
                    .GetDouble();

                double pastPrecipitation = 0.0;
                double antecedentPrecipIndex = 0.0;
                const double k = 0.85; // decay coefficient
                int offsetSeconds = doc.RootElement.TryGetProperty("utc_offset_seconds", out var offsetElem) ? offsetElem.GetInt32() : 0;
                var utcOffset = TimeSpan.FromSeconds(offsetSeconds);

                var dailyHistory = new List<DailyPrecipitation>();
                if (doc.RootElement.TryGetProperty("daily", out var dailyElem) &&
                    dailyElem.TryGetProperty("precipitation_sum", out var precipArray))
                {
                    var timeArray = dailyElem.TryGetProperty("time", out var tArr) ? tArr : default;

                    // Prendiamo tutti i giorni restituiti (solitamente 7 passati + oggi)
                    int count = precipArray.GetArrayLength();
                    for (int i = 0; i < count; i++)
                    {
                        var val = precipArray[i];
                        if (val.ValueKind != JsonValueKind.Null)
                        {
                            double dailyMm = val.GetDouble();
                            pastPrecipitation += (i < count - 1) ? dailyMm : 0; // Escludiamo oggi dal calcolo API antecedente se necessario, o includiamo tutto? 
                            // Il calcolo originale faceva: antecedentPrecipIndex = (k * antecedentPrecipIndex) + dailyMm;
                            // Ripristiniamo la logica originale di calcolo dell'indice ma fixiamo le date.
                            
                            if (i < count - 1) // Gli ultimi 7 giorni (escluso oggi) per l'indice antecedente
                            {
                                antecedentPrecipIndex = (k * antecedentPrecipIndex) + dailyMm;
                            }

                            // Aggiunta allo storico
                            string dateStr = $"Day {i+1}";
                            if (timeArray.ValueKind == JsonValueKind.Array && i < timeArray.GetArrayLength())
                            {
                                long unix = timeArray[i].GetInt64();
                                // Applichiamo l'offset locale per evitare l'errore del giorno precedente dovuto al fuso orario
                                dateStr = DateTimeOffset.FromUnixTimeSeconds(unix).ToOffset(utcOffset).ToString("yyyy-MM-dd");
                            }
                            dailyHistory.Add(new DailyPrecipitation(dateStr, Math.Round(dailyMm, 1)));
                        }
                    }
                }

                // Normalizza: API >= 80 mm = score 100
                int apiScore = (int)Math.Clamp((antecedentPrecipIndex / 80.0) * 100.0, 0, 100);
                
                // Normalizza: intensità attuale >= 30 mm/h = score 100
                int currentRainScore = (int)Math.Clamp((mmh / 30.0) * 100.0, 0, 100);

                var result = new WeatherData(
                    mmh, 
                    pastPrecipitation, 
                    antecedentPrecipIndex, 
                    apiScore, 
                    currentRainScore, 
                    "Open-Meteo",
                    dailyHistory
                );

                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(15));
                return result;
            }
            catch (Exception ex)
            {
                _log.LogWarning("WeatherService API fallita: {msg}", ex.Message);
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        }

    }
}
