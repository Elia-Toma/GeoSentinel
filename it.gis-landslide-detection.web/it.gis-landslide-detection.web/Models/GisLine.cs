using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace it.gis_landslide_detection.web.Models
{
    [Table("gis_lines")]
    public class GisLine
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("type")]
        public string? Type { get; set; } // e.g. "River", "Road"

        [Column("geom", TypeName = "geometry")]
        public Geometry? Geom { get; set; }
    }
}
