using System;
using System.Collections.Generic;

namespace WebsiteBackendFunctions.Models
{
    public class AvailabilityViewModel
    {
        public int? HutId { get; set; }
        public DateTime Date { get; set; }
        public DateTime? LastUpdated { get; set; }
        public int? TotalFreeBeds { get; set; }
        public int? TotalBeds { get; set; }
        public bool HutClosed { get; set; }
        public List<RoomAvailabilityViewModel> RoomAvailabilities { get; set; }
    }

    public class RoomAvailabilityViewModel
    {
        public string BedCategory { get; set; }
        public int FreeBeds { get; set; }
        public int TotalBeds { get; set; }
    }
}
