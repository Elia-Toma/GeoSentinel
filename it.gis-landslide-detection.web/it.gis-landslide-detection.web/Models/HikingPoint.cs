using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace it.gis_landslide_detection.web.Models;

[Table("hiking_points")]
public class HikingPoint
{
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("type")]
    public string? Type { get; set; }

    [Column("geom", TypeName = "geometry")]
    public Geometry? Geom { get; set; }
}