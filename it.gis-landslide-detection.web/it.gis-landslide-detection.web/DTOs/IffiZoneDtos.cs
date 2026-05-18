namespace it.gis_landslide_detection.web.DTOs
{
    public class IffiZoneDto
    {
        public int Id { get; set; }
        public string? IdFrana { get; set; }
        public string? NomeTipo { get; set; }
        public string? GeoJson { get; set; }
    }

    public class IffiZoneUpsertDto
    {
        public string? IdFrana { get; set; }
        public string? NomeTipo { get; set; }
        public string? GeoJson { get; set; }
    }
}
