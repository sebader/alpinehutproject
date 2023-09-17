using System;
using System.ComponentModel.DataAnnotations;

namespace WebsiteBackendFunctions.Models
{
    public class FreeBedUpdateSubscription
    {
        [Required]
        [RegularExpression(@"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$")]
        [MaxLength(100)]
        public string EmailAddress { get; set; }

        public int? HutId { get; set; }

        [Required]
        public DateTime? Date{ get; set; }
        
        public bool? Notified { get; set; }
    }
}
