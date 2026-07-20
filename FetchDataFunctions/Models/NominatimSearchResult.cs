namespace FetchDataFunctions.Models;

public class NominatimSearchResult
{
    public string osm_type { get; set; } = string.Empty;
    public string[] boundingbox { get; set; } = [];
    public string lat { get; set; } = string.Empty;
    public string lon { get; set; } = string.Empty;
    public string display_name { get; set; } = string.Empty;

    public string category { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public float importance { get; set; }
}
