namespace FetchDataFunctions.Models.HuettenHoliday;

public class Cabin
{
    public int id { get; set; }
    public int? user_id { get; set; }
    public int? region_id { get; set; }
    public int? country_id { get; set; }
    public int? reservation_type_id { get; set; }
    public string name { get; set; }
    public string slug { get; set; }
    public string? code { get; set; }
    public Description? description { get; set; }
    public string? website { get; set; }
    public int? invoice_number { get; set; }
    public string? checkin_from { get; set; }
    public string? checkin_to { get; set; }
    public double? altitude { get; set; }
    public double? latitude { get; set; }
    public double? longitude { get; set; }
    public int? cancellation_days { get; set; }
    public string? deposit_amount { get; set; }
    public string? halfboard_amount { get; set; }
    public string? routes { get; set; }
    public string? reachable_from { get; set; }
    public bool is_delete { get; set; }
    public bool is_neighbour { get; set; }
    public bool is_available { get; set; }
    public string? created_at { get; set; }
    public string? updated_at { get; set; }
    public bool is_private { get; set; }
    public bool email_inquiry { get; set; }
    public bool has_checkin_information { get; set; }
    public int? club_id { get; set; }
    public bool auto_distribute_managed_bookings { get; set; }
    public Region? region { get; set; }
    public Facilities[]? facilities { get; set; }
    public Country country { get; set; }
    public Seasons[]? seasons { get; set; }
    public Rooms[]? rooms { get; set; }
    public Reservation_type? reservation_type { get; set; }
}

public class Description
{
    public string de { get; set; }
    public string en { get; set; }
    public string it { get; set; }
}

public class Region
{
    public int id { get; set; }
    public Name name { get; set; }
    public string slug { get; set; }
}

public class Name
{
    public string en { get; set; }
    public string de { get; set; }
    public string it { get; set; }
}

public class Facilities
{
    public int id { get; set; }
    public int cabin_id { get; set; }
    public int facility_id { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
}

public class Country
{
    public int id { get; set; }
    public string code { get; set; }
    public Name1 name { get; set; }
    public string slug { get; set; }
    public string preffix { get; set; }
}

public class Name1
{
    public string en { get; set; }
    public string de { get; set; }
    public string it { get; set; }
}

public class Seasons
{
    public int id { get; set; }
    public int cabin_id { get; set; }
    public string slug { get; set; }
    public int year { get; set; }
    public string season_open { get; set; }
    public string season_close { get; set; }
    public bool is_delete { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
    public bool ms_active { get; set; }
    public Rest_days[] rest_days { get; set; }
}

public class Rest_days
{
    public int id { get; set; }
    public int cabin_season_id { get; set; }
    public string slug { get; set; }
    public bool active { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
}

public class Rooms
{
    public int id { get; set; }
    public int cabin_id { get; set; }
    public int room_id { get; set; }
    public bool is_delete { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
    public Room room { get; set; }
    public Details details { get; set; }
    public Cabin_room_availability_option cabin_room_availability_option { get; set; }
    public Options options { get; set; }
}

public class Room
{
    public int id { get; set; }
    public Name2 name { get; set; }
    public string slug { get; set; }
    public string abbreviation { get; set; }
    public string type { get; set; }
    public int capacity { get; set; }
}

public class Name2
{
    public string en { get; set; }
    public string de { get; set; }
    public string it { get; set; }
}

public class Details
{
    public int id { get; set; }
    public int cabin_room_id { get; set; }
    public int places { get; set; }
    public int guest_inquiry_starts_from { get; set; }
    public int guest_inquiry_starts_at { get; set; }
    public int mschool_inquiry_starts_from { get; set; }
    public int mschool_inquiry_starts_at { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
}

public class Cabin_room_availability_option
{
    public int cabin_room_id { get; set; }
    public object? availability_option_id { get; set; }
}

public class Options
{
    public int id { get; set; }
    public int cabin_room_id { get; set; }
    public int room_option_id { get; set; }
    public int min_places { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
    public Option option { get; set; }
}

public class Option
{
    public int id { get; set; }
    public Name3 name { get; set; }
    public string slug { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
}

public class Name3
{
    public string en { get; set; }
    public string de { get; set; }
    public string it { get; set; }
}

public class Reservation_type
{
    public int id { get; set; }
    public Name4 name { get; set; }
    public Description1 description { get; set; }
    public string icon { get; set; }
    public string slug { get; set; }
}

public class Name4
{
    public string en { get; set; }
    public string de { get; set; }
    public string it { get; set; }
}

public class Description1
{
    public string en { get; set; }
    public string de { get; set; }
    public string it { get; set; }
}

public class Images
{
    public int id { get; set; }
    public int cabin_id { get; set; }
    public int image_type_id { get; set; }
    public object title { get; set; }
    public string url { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
}

public class Gallery_images
{
    public int id { get; set; }
    public int cabin_id { get; set; }
    public int image_type_id { get; set; }
    public object title { get; set; }
    public string url { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
}

public class Title_image
{
    public int id { get; set; }
    public int cabin_id { get; set; }
    public int image_type_id { get; set; }
    public object title { get; set; }
    public string url { get; set; }
    public string created_at { get; set; }
    public string updated_at { get; set; }
}

