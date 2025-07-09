namespace FetchDataFunctions.Models.HuettenHoliday;

public class GetMonthAvailabilityPayload
{
    public int cabinId { get; set; }
    public SelectedMonth selectedMonth { get; set; }
    public bool multipleCalendar { get; set; } = false;
}

public class SelectedMonth
{
    public int monthNumber { get; set; }
    public int year { get; set; }
}