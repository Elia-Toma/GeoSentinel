using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System;
using System.Collections.Generic;

namespace it.gis_landslide_detection.web.Helpers
{
    public static class GeoJsonFormatter
    {
        public static FeatureCollection ToFeatureCollection<T>(IEnumerable<T> entities, Func<T, Geometry?> geometrySelector, Func<T, Dictionary<string, object>> propertiesSelector)
        {
            var featureCollection = new FeatureCollection();
            foreach (var entity in entities)
            {
                var geom = geometrySelector(entity);
                if (geom != null)
                {
                    var attributes = new AttributesTable(propertiesSelector(entity));
                    var feature = new Feature(geom, attributes);
                    featureCollection.Add(feature);
                }
            }
            return featureCollection;
        }

        public static Feature? ToFeature<T>(T entity, Func<T, Geometry?> geometrySelector, Func<T, Dictionary<string, object>> propertiesSelector)
        {
            var geom = geometrySelector(entity);
            if (geom == null) return null;
            var attributes = new AttributesTable(propertiesSelector(entity));
            return new Feature(geom, attributes);
        }

        public static string Format(object geoJsonObject)
        {
            var writer = new GeoJsonWriter();
            return writer.Write(geoJsonObject);
        }
    }
}
