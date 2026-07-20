using System.Text.RegularExpressions;

namespace FetchDataFunctions.Models;

public partial class Hut
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Link { get; set; }
    public string? HutWebsite { get; set; }
    public string? Country { get; set; }
    public string? Region { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? Altitude { get; set; }
    public bool? Enabled { get; set; }
    public DateTime? LastUpdated { get; set; }
    public DateTime? Added { get; set; }
    public DateTime? Activated { get; set; }

    public string? Source { get; set; }

    public bool? ManuallyEdited { get; set; }
    public bool? IsDeleted { get; set; }

    public virtual ICollection<Availability> Availability { get; set; } = new HashSet<Availability>();

    [GeneratedRegex(@"http[s]{0,1}://(www){0,1}\.*")]
    private static partial Regex WebsiteSchemeRegex();

    public string? ShortHutWebsite()
    {
        if (!string.IsNullOrEmpty(HutWebsite))
        {
            return WebsiteSchemeRegex().Replace(HutWebsite.ToLowerInvariant(), "");
        }

        return null;
    }
}
