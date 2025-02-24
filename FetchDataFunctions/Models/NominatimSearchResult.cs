using Newtonsoft.Json;

namespace FetchDataFunctions.Models
{
    public class NominatimSearchResult
    {
        public string osm_type { get; set; }
        public string[] boundingbox { get; set; }
        public string lat { get; set; }
        public string lon { get; set; }
        public string display_name { get; set; }

        public string category { get; set; }
        public string type { get; set; }
        public float importance { get; set; }
    }
}