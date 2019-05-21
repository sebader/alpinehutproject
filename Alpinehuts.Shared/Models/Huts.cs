using System;
using System.Collections.Generic;

namespace Shared.Models
{
    public partial class Hut
    {
        public Hut()
        {
            Availability = new HashSet<Availability>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Link { get; set; }
        public string Language { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public string Coordinates { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool? Enabled { get; set; }
        public DateTime? LastUpdated { get; set; }

        public virtual ICollection<Availability> Availability { get; set; }
    }
}
