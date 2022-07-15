using System;

namespace Alpinehuts.Shared.ViewModels
{
    public class AvailabilityIntermediaryModel
    {
        public int? HutId { get; set; }
        public int? AvailabilityId { get; set; }
        public DateTime? Date { get; set; }
        public string BedCategory { get; set; }
        public int? FreeRoom { get; set; }
        public int? TotalRoom { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}
