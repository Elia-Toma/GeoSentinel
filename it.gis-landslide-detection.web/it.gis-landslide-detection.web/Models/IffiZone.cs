using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace it.gis_landslide_detection.web.Models
{
    [Table("landslide_zones")] // Mappatura alla tabella reale
    public class IffiZone
    {
        [Key]
        [Column("ogc_fid")] // Chiave primaria creata da ogr2ogr
        public int Id { get; set; }

        [Column("id_frana")]
        public string? IdFrana { get; set; }

        [Column("nome_tipo")]
        public string? NomeTipo { get; set; } // Equivalente a Pericolosita/TipoFrana

        [Column("geom", TypeName = "geometry")]
        public Geometry? Geom { get; set; }
    }
}