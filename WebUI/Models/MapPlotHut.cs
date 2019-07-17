using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebUI.Models
{
    public class MapPlotHut
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string OnlineBookingLink { get; set; }
        public string HutWebsiteLink { get; set; }
        public bool Enabled { get; set; }
        public int? FreeBeds { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
