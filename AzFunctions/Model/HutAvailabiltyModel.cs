using System;
using System.Collections.Generic;

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AlpineHutsProject.Model
{
    public class Day
    {
        public DateTime? Date { get; set; } = null;
        public List<RoomAvailability> Rooms { get; set; } = new List<RoomAvailability>();
    }

    public class RoomAvailability
    {
        [JsonProperty("bedCategoryId")]
        public int? BedCategoryId { get; set; }

        [JsonProperty("freeRoom")]
        public int? FreeRoom { get; set; }

        [JsonProperty("totalRoom")]
        public int? TotalRoom { get; set; }

        [JsonProperty("BedCategory")]
        public string BedCategory
        {
            get
            {
                switch (BedCategoryId)
                {
                    case 1: return "Massenlager";
                    case 2: return "Zimmer";
                    case 4: return "Matratzenlager";
                    case 5: return "Mehrbettzimmer";
                    case 7: return "Matratzenlager";
                    case 8: return "Mehrbettzimmer";
                    case 9: return "Zweierzimmer";
                    default:
                        return null;
                }
            }
        }
    }

    public class RoomDayAvailability
    {
        [JsonProperty("hutBedCategoryId")]
        public int HutBedCategoryId { get; set; }

        [JsonProperty("bedCategoryId")]
        public int BedCategoryId { get; set; }

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

        [JsonProperty("hutDefaultLanguage")]
        public string HutDefaultLanguage { get; set; }

        [JsonProperty("reservedRoomsRatio")]
        public double ReservedRoomsRatio { get; set; }
    }


}
