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
        public int Population { get; set; } // Riusato come RiskLevel (0-100): coerente con scala IFFI (ColamentoRapido=100, CrolloRibaltamento=80, Scivolamento=60, Complesso=40)

        [Column("geom", TypeName = "geometry")]
        public Geometry? Geom { get; set; }
    }
}
