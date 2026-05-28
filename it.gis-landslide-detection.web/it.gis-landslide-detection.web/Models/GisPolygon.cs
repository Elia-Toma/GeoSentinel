using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace it.gis_landslide_detection.web.Models
{
    [Table("gis_polygons")]
    public class GisPolygon
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("population")]
        public int Population { get; set; } // Used for dynamic styling intensity

        [Column("geom", TypeName = "geometry")]
        public Geometry? Geom { get; set; }
    }
}
