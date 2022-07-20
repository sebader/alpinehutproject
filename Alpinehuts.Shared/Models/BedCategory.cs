using System.Collections.Generic;

namespace Shared.Models
{
    public partial class BedCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<Availability> Availabilities { get; set; }

        public int? SharesNameWithBedCateogryId { get; set; }
        public virtual BedCategory SharesNameWith { get; set; }
        public virtual ICollection<BedCategory> SameNamesAsThis { get; set; }

        public string CommonName
        {
            get
            {
                return SharesNameWith != null ? SharesNameWith.Name : Name;
            }
        }
    }
}
