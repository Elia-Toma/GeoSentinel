using System;
using System.Collections.Generic;
using Xunit;
using it.gis_landslide_detection.web.Services;
using it.gis_landslide_detection.web.Models;

namespace it.gis_landslide_detection.backtest;

public class HazardScoreBacktest
{
    private readonly IHazardScoreEngine _engine;

    public HazardScoreBacktest()
    {
        _engine = new HazardScoreEngine();
    }

    public static IEnumerable<object[]> LandslideEventsData =>
        new List<object[]>
        {
            // [0] Nome Evento
            // [1] Date
            // [2] IffiScore
            // [3] IffiTipo
            // [4] SoilMoistureScore
            // [5] ApiScore
            // [6] RainScore
            // [7] PrecipMmh
            // [8] IsSummer (per simulare il referenceDate)
            // [9] Livelli ammessi
            // [10] Override attesi

            // 1. Sarno (SA) — 5 maggio 1998
            new object[] { 
                "Sarno (SA) 1998", new DateTime(1998, 5, 5), 
                100.0, IffiHazardTypes.ColamentoRapido, 
                95, 100, 83, 25.0, false,
                new[] { "CRITICAL" }, 
                new[] { "none" } // Nessun override atteso (il livello base è già alto e la pioggia non è > 50)
            },

            // 2. Giampilieri, Messina — 1 ottobre 2009
            new object[] { 
                "Giampilieri (ME) 2009", new DateTime(2009, 10, 1), 
                100.0, IffiHazardTypes.ColamentoRapido, 
                55, 40, 100, 120.0, false,
                new[] { "CRITICAL" }, 
                new[] { "flash" } // Atteso flash override (precip > 50)
            },

            // 3. Ischia / Casamicciola — 26 novembre 2022
            new object[] { 
                "Ischia 2022", new DateTime(2022, 11, 26), 
                100.0, IffiHazardTypes.ColamentoRapido, 
                75, 65, 100, 52.0, false,
                new[] { "CRITICAL" }, 
                new[] { "flash" } // Atteso flash override
            },

            // 4. Marche / Cantiano — 15 settembre 2022
            new object[] { 
                "Cantiano (PU) 2022", new DateTime(2022, 9, 15), 
                100.0, IffiHazardTypes.ColamentoRapido, 
                30, 15, 100, 95.0, true,
                new[] { "CRITICAL" }, 
                new[] { "flash" } // Atteso flash override (R1) ed estate (R3 bilancia i pesi nel calcolo base)
            },

            // 5. Emilia-Romagna — maggio 2023
            new object[] { 
                "Emilia-Romagna 2023", new DateTime(2023, 5, 16), 
                60.0, IffiHazardTypes.ScivolamentoRotazionaleTraslativo, 
                98, 100, 50, 15.0, false,
                new[] { "HIGH", "CRITICAL" }, 
                new[] { "saturation" } // Atteso saturation floor (R2) poiché sat index sarà altissimo
            },

            // 6. Acquasanta Terme (AP) — novembre 2013
            new object[] { 
                "Acquasanta Terme 2013", new DateTime(2013, 11, 25), 
                40.0, IffiHazardTypes.Complesso, 
                95, 100, 60, 18.0, false,
                new[] { "HIGH" }, 
                new[] { "saturation" } // Atteso saturation floor (R2)
            },

            // 7. Valtellina / Val Pola — luglio 1987
            new object[] { 
                "Valtellina 1987", new DateTime(1987, 7, 28), 
                60.0, IffiHazardTypes.ScivolamentoRotazionaleTraslativo, 
                90, 95, 100, 45.0, true,
                new[] { "HIGH", "CRITICAL" }, 
                new[] { "saturation" } // Atteso saturation floor (R2)
            }
        };

    public static IEnumerable<object[]> ControlEventData =>
        new List<object[]>
        {
            // Controllo Negativo: Appennino marchigiano, giornata serena d'estate
            new object[] { 
                "Controllo Negativo (Sereno)", new DateTime(2025, 8, 15), 
                60.0, IffiHazardTypes.ScivolamentoRotazionaleTraslativo, 
                15, 5, 0, 0.0, true,
                new[] { "LOW" }, 
                new[] { "none" }
            }
        };

    [Theory]
    [MemberData(nameof(LandslideEventsData))]
    public void TestHistoricalCatastrophes(
        string eventName, DateTime referenceDate, 
        double iffiScore, string iffiTipo, 
        int soilMoistureScore, int apiScore, int rainScore, double precipMmh, bool isSummer,
        string[] acceptableLevels, string[] expectedOverrides)
    {
        // Act
        var result = _engine.Calculate(
            iffiScore, iffiTipo, 
            soilMoistureScore, apiScore, rainScore, precipMmh, 
            false, referenceDate);

        // Assert
        Assert.Contains(result.HazardLevel, acceptableLevels);
        
        // Verifica dei meccanismi di override
        if (Array.Exists(expectedOverrides, o => o == "flash"))
        {
            // Se precipMmh > 50, siamo in un flash event. L'override viene "applicato" solo se il floor alza il valore
            Assert.True(precipMmh > 50.0, $"Expected precipMmh > 50 for flash event in {eventName}");
            if (result.BaseHazard * 0.75 > result.BaseHazard * result.TriggerMultiplier)
            {
                Assert.True(result.FlashOverrideApplied, $"Expected Flash Override to be applied for {eventName}.");
            }
        }
        else if (Array.Exists(expectedOverrides, o => o == "saturation"))
        {
            Assert.True(result.SaturationIndex > 80.0, $"Expected Saturation Index > 80 for {eventName}, but got {result.SaturationIndex}.");
        }
        
        // Verifichiamo che i pesi stagionali estivi siano applicati correttamente per i tipi compatibili
        if (isSummer && (iffiTipo == IffiHazardTypes.ColamentoRapido || iffiTipo == IffiHazardTypes.ScivolamentoRotazionaleTraslativo))
        {
            Assert.Equal(0.45, result.WRain); // wRain in estate passa a 0.45
            Assert.Equal(0.20, result.WSoil); // wSoil in estate passa a 0.20
        }
    }

    [Theory]
    [MemberData(nameof(ControlEventData))]
    public void TestControlEvent(
        string eventName, DateTime referenceDate, 
        double iffiScore, string iffiTipo, 
        int soilMoistureScore, int apiScore, int rainScore, double precipMmh, bool isSummer,
        string[] acceptableLevels, string[] expectedOverrides)
    {
        // Act
        var result = _engine.Calculate(
            iffiScore, iffiTipo, 
            soilMoistureScore, apiScore, rainScore, precipMmh, 
            false, referenceDate);

        // Assert
        Assert.Contains(result.HazardLevel, acceptableLevels);
        Assert.False(result.FlashOverrideApplied);
        Assert.False(result.SaturationFloorApplied);
    }
}
