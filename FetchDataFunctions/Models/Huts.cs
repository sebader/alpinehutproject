using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FetchDataFunctions.Models
{
    public partial class Hut
    {
        public Hut()
        {
            Availability = new HashSet<Availability>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string? Link { get; set; }
        public string? HutWebsite { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? Altitude { get; set; }
        public bool? Enabled { get; set; }
        public DateTime? LastUpdated { get; set; }
        public DateTime? Added { get; set; }
        public DateTime? Activated { get; set; }
        
        public bool? ManuallyEdited { get; set; }

        public virtual ICollection<Availability> Availability { get; set; }

        public string? ShortHutWebsite()
        {
            if (!string.IsNullOrEmpty(HutWebsite))
            {
                var r = new Regex(@"http[s]{0,1}://(www){0,1}\.*");
                return r.Replace(HutWebsite.ToLower(), "");
            }
            else
            {
                return null;
            }
        }
    }
}
