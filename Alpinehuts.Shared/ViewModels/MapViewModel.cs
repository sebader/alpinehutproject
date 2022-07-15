using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alpinehuts.Shared.ViewModels
{
    public class MapViewModel
    {
        public int HutId { get; set; }
        public string HutName { get; set; }
        public bool HutEnabled { get; set; }
        public string HutWebsite { get; set; }
        public string BookingLink { get; set; }
        public int? FreeBeds { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime Date { get; set; }
        public DateTime? LastUpdated { get; set; }
        public List<AvailabilityIntermediaryModel> Availabilities { get; set; }
    }
}
