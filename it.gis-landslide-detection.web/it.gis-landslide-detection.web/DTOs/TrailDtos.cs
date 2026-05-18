namespace it.gis_landslide_detection.web.DTOs
{
    public class TrailDto
    {
        public long Id { get; set; }
        public long? OsmId { get; set; }
        public string? Name { get; set; }
        public string? SacScale { get; set; }
        public string? GeoJson { get; set; }
    }

    public class TrailUpsertDto
    {
        public long? OsmId { get; set; }
        public string? Name { get; set; }
        public string? SacScale { get; set; }
        public string? GeoJson { get; set; }
    }
}
