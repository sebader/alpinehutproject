using System;
using System.Collections.Generic;

namespace FetchDataFunctions.Models;

public class AvailabilityData
{
    public Dictionary<string, int> freeBedsPerCategory { get; set; }
    public int? freeBeds { get; set; }
    public string hutStatus { get; set; }
    public DateTime date { get; set; }
    public string dateFormatted { get; set; }
    public int? totalSleepingPlaces { get; set; }
    public string percentage { get; set; }
    
    /// <summary>
    /// Only the date part of the DateTime object, without time.
    /// </summary>
    public DateTime DateNormalized => new(date.Year, date.Month, date.Day);

    public bool HutClosed => hutStatus.Equals("CLOSED", StringComparison.OrdinalIgnoreCase) || percentage.Equals("CLOSED", StringComparison.OrdinalIgnoreCase);
}