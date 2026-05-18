namespace it.gis_landslide_detection.web.DTOs
{
    public class HikingPointDto
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? GeoJson { get; set; }
    }

    public class HikingPointUpsertDto
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? GeoJson { get; set; }
    }
}
