using System;

namespace FetchDataFunctions.Models;

public record HutInfoV2
{
    public string? hutWebsite { get; set; }
    public int hutId { get; set; }
    public string tenantCode { get; set; } = string.Empty;
    public bool hutUnlocked { get; set; }
    public int maxNumberOfNights { get; set; }
    public string hutName { get; set; } = string.Empty;
    public string? hutWarden { get; set; }
    public string? phone { get; set; }
    public string? coordinates { get; set; }
    public string? altitude { get; set; }
    public string? totalBedsInfo { get; set; }
    public string tenantCountry { get; set; } = string.Empty;
    public Picture? picture { get; set; }
    public string[] hutLanguages { get; set; } = [];
    public HutBedCategory[] hutBedCategories { get; set; } = [];
    public string providerName { get; set; } = string.Empty;
    public HutGeneralDescription[] hutGeneralDescriptions { get; set; } = [];
    public string? supportLink { get; set; }
    public bool? waitingListEnabled { get; set; }

    public double? Latitude
    {
        get
        {
            // Coordinates are in the format "lat, lon" or lat/lon"
            var parts = coordinates?.Split((char[]?)[',', '/'], StringSplitOptions.RemoveEmptyEntries) ?? [];
            return parts.Length == 2 ? (double.TryParse(parts[0].Trim(), out var result) ? result : null) : null;
        }
    }

    public double? Longitude
    {
        get
        {
            // Coordinates are in the format "lat, lon" or lat/lon"
            var parts = coordinates?.Split((char[]?)[',', '/'], StringSplitOptions.RemoveEmptyEntries) ?? [];
            return parts.Length == 2 ? (double.TryParse(parts[1].Trim(), out var result) ? result : null) : null;
        }
    }

    public int? AltitudeInt
    {
        get
        {
            var trimmed = altitude?.Replace("H.ü.M", "")
                .Replace(".", "")
                .Replace("m ü M", "")
                .Replace("m", "")
                .Replace("M", "")
                .Trim();
            return int.TryParse(trimmed, out var result) ? result : null;
        }
    }

    public string? HutWebsiteNormalized
    {
        get
        {
            if (string.IsNullOrWhiteSpace(hutWebsite))
                return null;

            var normalized = hutWebsite.Trim().ToLowerInvariant();
            if (normalized.StartsWith("http://", StringComparison.Ordinal) || normalized.StartsWith("https://", StringComparison.Ordinal))
                return normalized;

            return "http://" + normalized;
        }
    }

    public string? CountryNormalized
    {
        get
        {
            if (string.IsNullOrWhiteSpace(tenantCountry))
                return null;

            return tenantCountry switch
            {
                "AT" => "Österreich",
                "CH" => "Schweiz",
                "DE" => "Deutschland",
                "IT" => "Italien",
                _ => tenantCountry
            };
        }
    }
}

public record Picture
{
    public string fileType { get; set; } = string.Empty;
    public string blobPath { get; set; } = string.Empty;
    public string fileName { get; set; } = string.Empty;
    public object fileData { get; set; } = null!;
}

public record HutBedCategory
{
    public int index { get; set; }
    public int categoryID { get; set; }
    public object[] rooms { get; set; } = [];
    public bool isVisible { get; set; }
    public int totalSleepingPlaces { get; set; }
    public string reservationMode { get; set; } = string.Empty;
    public HutBedCategoryLanguageData[] hutBedCategoryLanguageData { get; set; } = [];
    public bool isLinkedToReservation { get; set; }
    public int tenantBedCategoryId { get; set; }
}

public record HutBedCategoryLanguageData
{
    public string language { get; set; } = string.Empty;
    public string label { get; set; } = string.Empty;
    public string shortLabel { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
}

public record HutGeneralDescription
{
    public string description { get; set; } = string.Empty;
    public string language { get; set; } = string.Empty;
}