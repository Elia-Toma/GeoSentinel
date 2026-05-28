using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace it.gis_landslide_detection.web.Models
{
    [Table("gis_points")]
    public class GisPoint
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("type")]
        public string? Type { get; set; } // e.g. "Restaurant", "School"

        [Column("geom", TypeName = "geometry")]
        public Geometry? Geom { get; set; }
    }
}
