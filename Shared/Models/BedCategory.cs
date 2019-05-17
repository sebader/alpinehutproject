using System.Collections.Generic;

namespace Shared.Models
{
    public partial class BedCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<Availability> Availabilities { get; set; }
    }
}
