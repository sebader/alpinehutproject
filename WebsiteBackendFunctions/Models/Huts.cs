using System;

namespace WebsiteBackendFunctions.Models
{
    public class Hut
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Link { get; set; }
        public string HutWebsite { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? Altitude { get; set; }
        public bool? Enabled { get; set; }
        public DateTime? LastUpdated { get; set; }
        public DateTime? Added { get; set; }
        public DateTime? Activated { get; set; }
    }
}
