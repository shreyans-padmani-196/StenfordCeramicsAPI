using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Stenford.Common.Utility
{
    public record GeoPoint(decimal Latitude, decimal Longitude);

    public interface IMapLinkParser
    {
        Task<GeoPoint?> ParseAsync(string? mapUrl, CancellationToken ct = default);
        GeoPoint? Parse(string? mapUrl);
        Task<GeoPoint?> GeocodeAddressAsync(string? address, CancellationToken ct = default);
    }

    public class GoogleMapLinkParser : IMapLinkParser
    {
        private static readonly HttpClient _httpClient;
        private static readonly ConcurrentDictionary<string, GeoPoint> _cache = new();
        private static readonly SemaphoreSlim _throttler = new(1, 1);

        // Google Maps API Key
        private const string GoogleApiKey = "AIzaSyBKgPe-P7029JQIk9KYDT7Os4U96g5Mmbs";

        // Optional Secondary Fallback (LocationIQ)
        private const string LocationIQApiKey = "pk.5b8a7ee02fac0ddbf3b80b446419f4c5";

        static GoogleMapLinkParser()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        private static readonly GoogleMapLinkParser _defaultInstance = new GoogleMapLinkParser();
        public static IMapLinkParser Instance => _defaultInstance;

        public GeoPoint? Parse(string? mapUrl)
        {
            return ParseAsync(mapUrl).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Parses any Google Map link (e.g. https://maps.app.goo.gl/..., https://goo.gl/maps/..., or full URL)
        /// and extracts exact Lat/Lng coordinates. Returns null if not found.
        /// </summary>
        public async Task<GeoPoint?> ParseAsync(string? mapUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(mapUrl)) return null;

            mapUrl = mapUrl.Trim();

            // Check cache
            if (_cache.TryGetValue(mapUrl, out var cached))
                return cached;

            Console.WriteLine($"\n==================== [GoogleMapLinkParser START] ====================");
            Console.WriteLine($"[GoogleMapLinkParser] Input URL: {mapUrl}");

            try
            {
                string finalUrl = mapUrl;

                // 1. Resolve shortened URLs (maps.app.goo.gl / goo.gl / bit.ly etc.)
                if (mapUrl.Contains("goo.gl", StringComparison.OrdinalIgnoreCase) ||
                    mapUrl.Contains("maps.app", StringComparison.OrdinalIgnoreCase) ||
                    !mapUrl.Contains("/@") && !mapUrl.Contains("!3d"))
                {
                    try
                    {
                        var req = new HttpRequestMessage(HttpMethod.Get, mapUrl);
                        var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                        if (response.RequestMessage?.RequestUri != null)
                        {
                            finalUrl = response.RequestMessage.RequestUri.ToString();
                            Console.WriteLine($"[GoogleMapLinkParser] 1. Expanded URL: {finalUrl}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[GoogleMapLinkParser] URL Expansion Warning: {ex.Message}");
                    }
                }

                // 2. Extract coordinates directly from URL (Marker !3d/!4d, Query ?q=, or Center /@lat,lng)
                var coords = ExtractCoordinatesFromUrl(finalUrl) ?? ExtractCoordinatesFromUrl(mapUrl);
                if (coords != null)
                {
                    Console.WriteLine($"[GoogleMapLinkParser] 2. Exact Coordinates from URL => Lat: {coords.Latitude}, Lng: {coords.Longitude}");
                    _cache[mapUrl] = coords;
                    return coords;
                }

                // 3. Extract Place Name / Address from the Google Maps URL and Geocode
                var placeAddress = ExtractPlaceAddressFromUrl(finalUrl) ?? ExtractPlaceAddressFromUrl(mapUrl);
                if (!string.IsNullOrWhiteSpace(placeAddress))
                {
                    Console.WriteLine($"[GoogleMapLinkParser] 3. Extracted Place/Address: {placeAddress}");

                    // Geocode via Google Geocoding API
                    if (!string.IsNullOrWhiteSpace(GoogleApiKey))
                    {
                        var googleGeo = await GeocodeWithGoogleAsync(placeAddress, ct);
                        if (googleGeo != null)
                        {
                            Console.WriteLine($"[GoogleMapLinkParser] Exact Location (Google API) => Lat: {googleGeo.Latitude}, Lng: {googleGeo.Longitude}");
                            _cache[mapUrl] = googleGeo;
                            return googleGeo;
                        }
                    }

                    // Fallback to LocationIQ
                    if (!string.IsNullOrWhiteSpace(LocationIQApiKey))
                    {
                        var locationIqGeo = await GeocodeWithLocationIQAsync(placeAddress);
                        if (locationIqGeo != null)
                        {
                            Console.WriteLine($"[GoogleMapLinkParser] Exact Location (LocationIQ) => Lat: {locationIqGeo.Latitude}, Lng: {locationIqGeo.Longitude}");
                            _cache[mapUrl] = locationIqGeo;
                            return locationIqGeo;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleMapLinkParser] Exception: {ex.Message}");
            }

            Console.WriteLine("[GoogleMapLinkParser] No coordinates found. Returning NULL.");
            Console.WriteLine($"==================== [GoogleMapLinkParser END] ====================\n");
            return null;
        }

        /// <summary>
        /// Direct Geocoding for a text address (without map URL).
        /// </summary>
        public async Task<GeoPoint?> GeocodeAddressAsync(string? address, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            return await GeocodeWithGoogleAsync(address.Trim(), ct);
        }

        private static GeoPoint? ExtractCoordinatesFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string decoded = Uri.UnescapeDataString(url);

            // 1. Exact Place Marker: !3d{lat}!4d{lng}
            var match3d = Regex.Match(decoded, @"!3d(-?\d{1,2}(?:\.\d+)+)!4d(-?\d{1,3}(?:\.\d+)+)");
            if (match3d.Success &&
                decimal.TryParse(match3d.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lat3d) &&
                decimal.TryParse(match3d.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lng3d) &&
                IsValid(lat3d, lng3d))
            {
                return new GeoPoint(lat3d, lng3d);
            }

            // 2. Query coordinates: ?q=lat,lng or ?ll=lat,lng or ?daddr=lat,lng or destination=lat,lng
            var matchQ = Regex.Match(decoded, @"(?:[?&](?:q|query|ll|daddr|destination)=)(-?\d{1,2}(?:\.\d+)+)[,\s%2C]+(-?\d{1,3}(?:\.\d+)+)");
            if (matchQ.Success &&
                decimal.TryParse(matchQ.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal latQ) &&
                decimal.TryParse(matchQ.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lngQ) &&
                IsValid(latQ, lngQ))
            {
                return new GeoPoint(latQ, lngQ);
            }

            // 3. Viewport Center coordinates: /@lat,lng,zoom
            var matchAt = Regex.Match(decoded, @"/@(-?\d{1,2}(?:\.\d+)+),(-?\d{1,3}(?:\.\d+)+)");
            if (matchAt.Success &&
                decimal.TryParse(matchAt.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal latAt) &&
                decimal.TryParse(matchAt.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lngAt) &&
                IsValid(latAt, lngAt))
            {
                return new GeoPoint(latAt, lngAt);
            }

            return null;
        }

        private static string? ExtractPlaceAddressFromUrl(string url)
        {
            var match = Regex.Match(url, @"/maps/place/([^/@?]+)");
            if (match.Success)
            {
                string raw = Uri.UnescapeDataString(match.Groups[1].Value.Replace('+', ' '));
                raw = Regex.Replace(raw, @"^[A-Z0-9]{4,8}\+[A-Z0-9]{2,4}\s*", "", RegexOptions.IgnoreCase).Trim();
                return raw;
            }
            return null;
        }

        private static async Task<GeoPoint?> GeocodeWithGoogleAsync(string address, CancellationToken ct = default)
        {
            try
            {
                string url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={GoogleApiKey}";
                var res = await _httpClient.GetAsync(url, ct);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                    {
                        var location = results[0].GetProperty("geometry").GetProperty("location");
                        decimal lat = location.GetProperty("lat").GetDecimal();
                        decimal lng = location.GetProperty("lng").GetDecimal();

                        if (IsValid(lat, lng))
                            return new GeoPoint(lat, lng);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleGeocode] Error: {ex.Message}");
            }
            return null;
        }

        private static async Task<GeoPoint?> GeocodeWithLocationIQAsync(string rawAddress)
        {
            var rawParts = rawAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanParts = new List<string>();

            foreach (var p in rawParts)
            {
                string cleaned = p.Trim();
                cleaned = Regex.Replace(cleaned, @"\b(infront of|near|opp|opposite|behind|beside|at)\b.*", "", RegexOptions.IgnoreCase).Trim();
                if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length >= 2)
                    cleanParts.Add(cleaned);
            }

            var queries = new List<string>
            {
                rawAddress,
                string.Join(", ", cleanParts)
            };

            if (cleanParts.Count >= 3)
            {
                queries.Add($"{cleanParts[0]}, {cleanParts[^2]}, {cleanParts[^1]}");
            }

            await _throttler.WaitAsync();
            try
            {
                foreach (var q in queries)
                {
                    try
                    {
                        string url = $"https://us1.locationiq.com/v1/search?key={LocationIQApiKey}&q={Uri.EscapeDataString(q)}&countrycodes=in&format=json&limit=1";
                        var res = await _httpClient.GetAsync(url);
                        if (res.IsSuccessStatusCode)
                        {
                            string json = await res.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                            {
                                var first = doc.RootElement[0];
                                if (decimal.TryParse(first.GetProperty("lat").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lat) &&
                                    decimal.TryParse(first.GetProperty("lon").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lon))
                                {
                                    if (IsValid(lat, lon)) return new GeoPoint(lat, lon);
                                }
                            }
                        }
                    }
                    catch { }

                    await Task.Delay(250);
                }
            }
            finally
            {
                _throttler.Release();
            }

            return null;
        }

        private static bool IsValid(decimal lat, decimal lng)
        {
            // Valid latitude (-90 to +90) and longitude (-180 to +180)
            return lat >= -90.0m && lat <= 90.0m && lng >= -180.0m && lng <= 180.0m && (lat != 0 || lng != 0);
        }
    }
}
