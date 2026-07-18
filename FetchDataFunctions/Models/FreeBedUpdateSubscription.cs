using System;

namespace FetchDataFunctions.Models
{
    public class FreeBedUpdateSubscription
    {
        public string EmailAddress { get; set; } = string.Empty;

        public int? HutId { get; set; }

        public DateTime? Date{ get; set; }
        
        public bool? Notified { get; set; }
    }
}
