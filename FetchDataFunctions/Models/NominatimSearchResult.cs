using Newtonsoft.Json;

namespace FetchDataFunctions.Models
{
    public class NominatimSearchResult
    {
        public string place_id { get; set; }
        public string licence { get; set; }
        public string osm_type { get; set; }
        public string osm_id { get; set; }
        public string[] boundingbox { get; set; }
        public string lat { get; set; }
        public string lon { get; set; }
        public string display_name { get; set; }

        [JsonProperty("class")]
        public string _class { get; set; }
        public string type { get; set; }
        public float importance { get; set; }
    }
}
