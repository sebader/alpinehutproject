using System;

namespace FetchDataFunctions.Models.HuettenHoliday;

public class AvailabilityResult
{
    public DateTime date { get; set; }
    public AvailabilityRooms[] rooms { get; set; }
    public int totalPlaces { get; set; }
}

public class AvailabilityRooms
{
    public int room_id { get; set; }
    public int places { get; set; }
    public int paid_places { get; set; }
    public int booked_places { get; set; }
}