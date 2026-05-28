using it.gis_landslide_detection.web.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace it.gis_landslide_detection.web.Data
{
    public static class GisDataSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

            if (!await context.GisPoints.AnyAsync())
            {
                context.GisPoints.AddRange(
                    new GisPoint { Name = "Ristorante Da Mario", Type = "Restaurant", Geom = geometryFactory.CreatePoint(new Coordinate(13.4, 43.1)) },
                    new GisPoint { Name = "Pizzeria Bella Napoli", Type = "Restaurant", Geom = geometryFactory.CreatePoint(new Coordinate(13.41, 43.12)) },
                    new GisPoint { Name = "Scuola Elementare Garibaldi", Type = "School", Geom = geometryFactory.CreatePoint(new Coordinate(13.39, 43.08)) },
                    new GisPoint { Name = "Liceo Scientifico", Type = "School", Geom = geometryFactory.CreatePoint(new Coordinate(13.45, 43.15)) }
                );
            }

            if (!await context.GisLines.AnyAsync())
            {
                context.GisLines.AddRange(
                    new GisLine { Name = "Fiume Tronto", Type = "River", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.3, 43.0), new Coordinate(13.5, 43.2), new Coordinate(13.7, 43.3) }) },
                    
                    // SS77 della Val di Chienti - Divisa nei suoi 3 segmenti reali tra gli incroci
                    new GisLine { Name = "SS77 della Val di Chienti - Tratto 1", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.1, 43.15), new Coordinate(13.3, 43.2) }) },
                    new GisLine { Name = "SS77 della Val di Chienti - Tratto 2", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.3, 43.2), new Coordinate(13.45, 43.25) }) },
                    new GisLine { Name = "SS77 della Val di Chienti - Tratto 3", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.45, 43.25), new Coordinate(13.6, 43.3) }) },

                    // SP 256 - Divisa nei suoi 2 segmenti reali tra gli incroci
                    new GisLine { Name = "SP 256 - Tratto 1", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.1, 43.15), new Coordinate(13.15, 43.05) }) },
                    new GisLine { Name = "SP 256 - Tratto 2", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.15, 43.05), new Coordinate(13.25, 43.0) }) },

                    // Strada Provinciale 78 - Divisa nei suoi 2 segmenti reali tra gli incroci
                    new GisLine { Name = "Strada Provinciale 78 - Tratto 1", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.3, 43.2), new Coordinate(13.35, 43.1) }) },
                    new GisLine { Name = "Strada Provinciale 78 - Tratto 2", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.35, 43.1), new Coordinate(13.4, 43.0) }) },

                    // Strada di Collegamento - Divisa nei suoi 2 segmenti reali tra gli incroci
                    new GisLine { Name = "Strada di Collegamento - Tratto 1", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.15, 43.05), new Coordinate(13.35, 43.1) }) },
                    new GisLine { Name = "Strada di Collegamento - Tratto 2", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.35, 43.1), new Coordinate(13.45, 43.25) }) },

                    // Tangenziale Macerata - Divisa nei suoi 2 segmenti reali tra gli incroci
                    new GisLine { Name = "Tangenziale Macerata - Tratto 1", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.45, 43.25), new Coordinate(13.5, 43.2) }) },
                    new GisLine { Name = "Tangenziale Macerata - Tratto 2", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.5, 43.2), new Coordinate(13.6, 43.15) }) },

                    // Via delle Vigne - Divisa nei suoi 2 segmenti reali tra gli incroci
                    new GisLine { Name = "Via delle Vigne - Tratto 1", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.4, 43.0), new Coordinate(13.5, 43.05) }) },
                    new GisLine { Name = "Via delle Vigne - Tratto 2", Type = "Road", Geom = geometryFactory.CreateLineString(new Coordinate[] { new Coordinate(13.5, 43.05), new Coordinate(13.6, 43.15) }) }
                );
            }

            if (!await context.GisPolygons.AnyAsync())
            {
                context.GisPolygons.AddRange(
                    new GisPolygon
                    {
                        Name = "Camerino",
                        Population = 6500,
                        Geom = geometryFactory.CreatePolygon(new Coordinate[] 
                        {
                            new Coordinate(13.0, 43.1), new Coordinate(13.1, 43.1), 
                            new Coordinate(13.1, 43.2), new Coordinate(13.0, 43.2), 
                            new Coordinate(13.0, 43.1) 
                        })
                    },
                    new GisPolygon
                    {
                        Name = "Macerata",
                        Population = 41000,
                        Geom = geometryFactory.CreatePolygon(new Coordinate[] 
                        {
                            new Coordinate(13.4, 43.2), new Coordinate(13.5, 43.2), 
                            new Coordinate(13.5, 43.3), new Coordinate(13.4, 43.3), 
                            new Coordinate(13.4, 43.2) 
                        })
                    }
                );
            }

            await context.SaveChangesAsync();
        }
    }
}
