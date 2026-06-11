using it.gis_landslide_detection.web.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace it.gis_landslide_detection.web.Data
{
    /// <summary>
    /// Popola il database con dati GIS di esempio centrati sull'area di Camerino / Sibillini.
    ///
    /// PUNTI: Baite e Punti di Ristoro
    /// LINEE: Sentieri montani (usati dal routing Dijkstra) e Torrenti/Fiumi
    /// POLIGONI: Zone di rischio frana con indice 0-100 coerente con la scala IFFI:
    ///   - ColamentoRapido      = 100 (Critico)
    ///   - CrolloRibaltamento   = 80  (Alto)
    ///   - Scivolamento         = 60  (Medio-Alto)
    ///   - Complesso            = 40  (Medio)
    ///   - Bassa pericolosità   = 20  (Basso)
    /// </summary>
    public static class GisDataSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            var gf = new GeometryFactory(new PrecisionModel(), 4326);

            // ── PUNTI: Baite e Punti di Ristoro (area Camerino / Sibillini) ─────────────
            if (!await context.GisPoints.AnyAsync())
            {
                context.GisPoints.AddRange(
                    // Baite
                    new GisPoint
                    {
                        Name = "Rifugio Fargno",
                        Type = "Baita",
                        Geom = gf.CreatePoint(new Coordinate(13.0600, 43.0750))
                    },
                    new GisPoint
                    {
                        Name = "Baita Monte Rotondo",
                        Type = "Baita",
                        Geom = gf.CreatePoint(new Coordinate(13.0480, 43.0620))
                    },
                    new GisPoint
                    {
                        Name = "Capanna Ghiacciaia",
                        Type = "Baita",
                        Geom = gf.CreatePoint(new Coordinate(13.1100, 43.1050))
                    },
                    // Punti di Ristoro
                    new GisPoint
                    {
                        Name = "Punto Ristoro Pintura di Bolognola",
                        Type = "PuntoRistoro",
                        Geom = gf.CreatePoint(new Coordinate(13.0850, 43.0650))
                    },
                    new GisPoint
                    {
                        Name = "Bar Ristoro Sassotetto",
                        Type = "PuntoRistoro",
                        Geom = gf.CreatePoint(new Coordinate(13.1250, 43.1350))
                    },
                    new GisPoint
                    {
                        Name = "Ostello della Montagna Camerino",
                        Type = "PuntoRistoro",
                        Geom = gf.CreatePoint(new Coordinate(13.0700, 43.1330))
                    }
                );
            }

            // ── LINEE: Sentieri montani e Torrenti ────────────────────────────────────────
            // I sentieri sono segmentati agli incroci per costruire una rete di routing Dijkstra.
            // Ogni nodo condiviso tra segmenti diversi crea un incrocio percorribile.
            //
            // Nodi chiave della rete:
            //   A = (13.035, 43.055)  → punto di partenza sentiero dal paese
            //   B = (13.055, 43.075)  → incrocio presso il Fargno
            //   C = (13.080, 43.090)  → incrocio centro area
            //   D = (13.105, 43.105)  → incrocio alta quota
            //   E = (13.060, 43.115)  → area nord-ovest
            //   F = (13.125, 43.070)  → area est
            //   G = (13.095, 43.050)  → area sud
            if (!await context.GisLines.AnyAsync())
            {
                context.GisLines.AddRange(
                    // ─── Sentiero dei Sibillini ───────────────────────────────────────────
                    new GisLine
                    {
                        Name = "Sentiero dei Sibillini - Tratto 1 (A→B)",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0350, 43.0550),
                            new Coordinate(13.0450, 43.0650),
                            new Coordinate(13.0550, 43.0750)
                        })
                    },
                    new GisLine
                    {
                        Name = "Sentiero dei Sibillini - Tratto 2 (B→C)",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0550, 43.0750),
                            new Coordinate(13.0680, 43.0820),
                            new Coordinate(13.0800, 43.0900)
                        })
                    },
                    new GisLine
                    {
                        Name = "Sentiero dei Sibillini - Tratto 3 (C→D)",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0800, 43.0900),
                            new Coordinate(13.0920, 43.0980),
                            new Coordinate(13.1050, 43.1050)
                        })
                    },

                    // ─── Sentiero del Fargno ──────────────────────────────────────────────
                    new GisLine
                    {
                        Name = "Sentiero del Fargno - Tratto 1 (A→G)",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0350, 43.0550),
                            new Coordinate(13.0650, 43.0500),
                            new Coordinate(13.0950, 43.0500)
                        })
                    },
                    new GisLine
                    {
                        Name = "Sentiero del Fargno - Tratto 2 (G→F)",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0950, 43.0500),
                            new Coordinate(13.1150, 43.0600),
                            new Coordinate(13.1250, 43.0700)
                        })
                    },

                    // ─── Sentiero Anello Camerino ─────────────────────────────────────────
                    new GisLine
                    {
                        Name = "Sentiero Anello Camerino - Tratto 1 (B→E)",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0550, 43.0750),
                            new Coordinate(13.0580, 43.0950),
                            new Coordinate(13.0600, 43.1150)
                        })
                    },
                    new GisLine
                    {
                        Name = "Sentiero Anello Camerino - Tratto 2 (E→D)",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0600, 43.1150),
                            new Coordinate(13.0820, 43.1100),
                            new Coordinate(13.1050, 43.1050)
                        })
                    },

                    // ─── Sentiero Cresta dei Sibillini ───────────────────────────────────
                    new GisLine
                    {
                        Name = "Sentiero Cresta - Tratto 1 (C→F)",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0800, 43.0900),
                            new Coordinate(13.1050, 43.0800),
                            new Coordinate(13.1250, 43.0700)
                        })
                    },
                    new GisLine
                    {
                        Name = "Sentiero del Ruscello",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0200, 43.1200),
                            new Coordinate(13.0400, 43.1100),
                            new Coordinate(13.0500, 43.0950)
                        })
                    },
                    new GisLine
                    {
                        Name = "Sentiero Panoramico",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0900, 43.1300),
                            new Coordinate(13.1000, 43.1400),
                            new Coordinate(13.1150, 43.1450)
                        })
                    },
                    new GisLine
                    {
                        Name = "Sentiero dei Lupi",
                        Type = "Sentiero",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0300, 43.0600),
                            new Coordinate(13.0500, 43.0550),
                            new Coordinate(13.0700, 43.0650)
                        })
                    },

                    // ─── Torrente Chienti ────────────────────────────────────────────────
                    new GisLine
                    {
                        Name = "Torrente Chienti",
                        Type = "Torrente",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0200, 43.0600),
                            new Coordinate(13.0400, 43.0700),
                            new Coordinate(13.0600, 43.0850),
                            new Coordinate(13.0800, 43.1000),
                            new Coordinate(13.1000, 43.1200)
                        })
                    },

                    // ─── Fiume Potenza ───────────────────────────────────────────────────
                    new GisLine
                    {
                        Name = "Fiume Potenza",
                        Type = "Fiume",
                        Geom = gf.CreateLineString(new[]
                        {
                            new Coordinate(13.0500, 43.1400),
                            new Coordinate(13.0800, 43.1300),
                            new Coordinate(13.1100, 43.1200),
                            new Coordinate(13.1400, 43.1100)
                        })
                    }
                );
            }

            // ── POLIGONI: Zone di rischio frana ──────────────────────────────────────────
            // Il campo "Population" è riusato come RiskLevel (0-100), scala coerente con IFFI:
            //   100 = Colamento rapido (Critico)  80 = Crollo/Ribaltamento (Alto)
            //    60 = Scivolamento (Medio-Alto)   40 = Complesso (Medio)   20 = Bassa (Basso)
            // Le zone hanno dimensioni diverse per testare il filtro per superficie.
            if (!await context.GisPolygons.AnyAsync())
            {
                context.GisPolygons.AddRange(
                    // Zona Critica — Versante Franoso Monte Rotondo
                    // Ampiezza: ~5 km² — rischio Critico (Colamento rapido = 100)
                    new GisPolygon
                    {
                        Name = "Zona Critica - Versante Monte Rotondo",
                        Population = 100, // RiskLevel: Colamento rapido
                        Geom = gf.CreatePolygon(new[]
                        {
                            new Coordinate(13.030, 43.055),
                            new Coordinate(13.060, 43.055),
                            new Coordinate(13.065, 43.080),
                            new Coordinate(13.035, 43.085),
                            new Coordinate(13.030, 43.055)
                        })
                    },
                    // Zona Alta — Cresta Sibillini
                    // Ampiezza: ~3 km² — rischio Alto (Crollo/Ribaltamento = 80)
                    new GisPolygon
                    {
                        Name = "Zona Alta - Cresta dei Sibillini",
                        Population = 80, // RiskLevel: Crollo/Ribaltamento
                        Geom = gf.CreatePolygon(new[]
                        {
                            new Coordinate(13.095, 43.090),
                            new Coordinate(13.125, 43.090),
                            new Coordinate(13.130, 43.110),
                            new Coordinate(13.100, 43.115),
                            new Coordinate(13.095, 43.090)
                        })
                    },
                    // Zona Medio-Alta — Valle del Chienti
                    // Ampiezza: ~7 km² — rischio Medio-Alto (Scivolamento = 60)
                    new GisPolygon
                    {
                        Name = "Zona Medio-Alta - Valle del Chienti",
                        Population = 60, // RiskLevel: Scivolamento rotazionale
                        Geom = gf.CreatePolygon(new[]
                        {
                            new Coordinate(13.045, 43.075),
                            new Coordinate(13.090, 43.075),
                            new Coordinate(13.095, 43.110),
                            new Coordinate(13.050, 43.115),
                            new Coordinate(13.045, 43.075)
                        })
                    },
                    // Zona Media — Altopiano di Fargno
                    // Ampiezza: ~4 km² — rischio Medio (Complesso = 40)
                    new GisPolygon
                    {
                        Name = "Zona Media - Altopiano di Fargno",
                        Population = 40, // RiskLevel: Complesso
                        Geom = gf.CreatePolygon(new[]
                        {
                            new Coordinate(13.070, 43.050),
                            new Coordinate(13.105, 43.050),
                            new Coordinate(13.110, 43.070),
                            new Coordinate(13.075, 43.072),
                            new Coordinate(13.070, 43.050)
                        })
                    },
                    // Zona Bassa — Area Pianeggiante Camerino
                    // Ampiezza: ~10 km² — rischio Basso = 20
                    new GisPolygon
                    {
                        Name = "Zona Bassa - Pianura Camerino",
                        Population = 20, // RiskLevel: Bassa pericolosità
                        Geom = gf.CreatePolygon(new[]
                        {
                            new Coordinate(13.020, 43.110),
                            new Coordinate(13.070, 43.110),
                            new Coordinate(13.080, 43.145),
                            new Coordinate(13.025, 43.150),
                            new Coordinate(13.020, 43.110)
                        })
                    }
                );
            }

            await context.SaveChangesAsync();
        }
    }
}
