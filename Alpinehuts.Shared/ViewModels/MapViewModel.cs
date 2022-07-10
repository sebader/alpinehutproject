using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alpinehuts.Shared.ViewModels
{
    public class MapViewModel
    {
        public int Hutid { get; set; }
        public string HutName { get; set; }
        public bool HutEnabled { get; set; }
        public string HutWebsite { get; set; }
        public int? FreeBeds { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime Date { get; set; }
        public DateTime? LastUpdated { get; set; }
        public List<AvailabilityViewModel> Availabilities { get; set; }
    }
}
