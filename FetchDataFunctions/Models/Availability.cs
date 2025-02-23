using System;

namespace FetchDataFunctions.Models
{
    public partial class Availability
    {
        public int AvailabilityId { get; set; }

        public int Hutid { get; set; }
        public DateTime Date { get; set; }
        public int BedCategoryId { get; set; }
        public int? TenantBedCategoryId { get; set; }
        public int? FreeRoom { get; set; }
        public int? TotalRoom { get; set; }
        public virtual BedCategory BedCategory { get; set; }
        public DateTime? LastUpdated { get; set; }

        public virtual Hut Hut { get; set; }
    }
}
