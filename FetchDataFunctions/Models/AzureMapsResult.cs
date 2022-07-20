using System;
using System.Collections.Generic;
using System.Text;

namespace AzureFunctions.Models
{
    public class AzureMapsResult
    {
        public Summary summary { get; set; }
        public Addresses[] addresses { get; set; }
    }

    public class Summary
    {
        public int queryTime { get; set; }
        public int numResults { get; set; }
    }

    public class Addresses
    {
        public Address address { get; set; }
        public string position { get; set; }
    }

    public class Address
    {
        public string countryCode { get; set; }
        public string countrySubdivision { get; set; }
        public string countrySecondarySubdivision { get; set; }
        public string municipality { get; set; }
        public string postalCode { get; set; }
        public string country { get; set; }
        public string countryCodeISO3 { get; set; }
        public string freeformAddress { get; set; }
        public Boundingbox boundingBox { get; set; }
    }

    public class Boundingbox
    {
        public string northEast { get; set; }
        public string southWest { get; set; }
        public string entity { get; set; }
    }

}
