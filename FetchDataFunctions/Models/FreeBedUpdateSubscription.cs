using System;

namespace FetchDataFunctions.Models
{
    public class FreeBedUpdateSubscription
    {
        public string EmailAddress { get; set; }

        public int? HutId { get; set; }

        public DateTime? Date{ get; set; }
        
        public bool? Notified { get; set; }
    }
}
