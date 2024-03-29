using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FetchDataFunctions.Models
{
    public class DayAvailability
    {
        public DateTime? Date { get; set; } = null;
        public List<RoomAvailability> Rooms { get; set; } = new List<RoomAvailability>();
        /// <summary>
        /// Returns True if all Rooms are closed on this day
        /// </summary>
        public bool HutClosed
        {
            get
            {
                return Rooms == null || Rooms.All(r => r.Closed);
            }
        }
    }

    public class RoomAvailability
    {
        [JsonProperty("bedCategoryId")]
        public int? BedCategoryId { get; set; }

        [JsonProperty("freeRoom")]
        public int? FreeRoom { get; set; }

        [JsonProperty("totalRoom")]
        public int? TotalRoom { get; set; }

        public bool Closed { get; set; }

    }
    public class RoomDayAvailability
    {
        [JsonProperty("hutBedCategoryId")]
        public int? HutBedCategoryId { get; set; }

        [JsonProperty("bedCategoryId")]
        public int? BedCategoryId { get; set; }

        [JsonProperty("bookingEnabled")]
        public bool BookingEnabled { get; set; }

        [JsonProperty("closed")]
        public bool Closed { get; set; }

        [JsonProperty("freeRoom")]
        public int FreeRoom { get; set; }

        [JsonProperty("futureHutOccupancyShown")]
        public bool FutureHutOccupancyShown { get; set; }

        [JsonProperty("reservationDate")]
        public string ReservationDate { get; set; }

        [JsonProperty("totalRoom")]
        public int TotalRoom { get; set; }

        [JsonProperty("reservedRoomsRatio")]
        public double ReservedRoomsRatio { get; set; }
    }
}
