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
    }

    public class GoogleMapLinkParser : IMapLinkParser
    {
        private static readonly HttpClient _httpClient;
        private static readonly ConcurrentDictionary<string, GeoPoint> _cache = new();
        private static readonly SemaphoreSlim _throttler = new(1, 1);

        // 🔑 LocationIQ API Key
        private const string LocationIQApiKey = "pk.5b8a7ee02fac0ddbf3b80b446419f4c5";
        private const string GoogleApiKey = "";

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

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        private static readonly GoogleMapLinkParser _defaultInstance = new GoogleMapLinkParser();
        public static IMapLinkParser Instance => _defaultInstance;

        public GeoPoint? Parse(string? mapUrl)
        {
            try
            {
                return Task.Run(() => ParseAsync(mapUrl)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleMapLinkParser] ❌ Sync Error: {ex.Message}");
                return null;
            }
        }

        public async Task<GeoPoint?> ParseAsync(string? mapUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(mapUrl))
                return null;

            mapUrl = mapUrl.Trim();
            Console.WriteLine($"\n==================== [GoogleMapLinkParser START] ====================");
            Console.WriteLine($"[GoogleMapLinkParser] 1. Input: {mapUrl}");

            if (_cache.TryGetValue(mapUrl, out var cached))
            {
                Console.WriteLine($"[GoogleMapLinkParser] ⚡ Served from Cache => Lat: {cached.Latitude}, Lng: {cached.Longitude}");
                return cached;
            }

            // 1. Direct coordinates string check: "21.7583, 72.1458"
            var directMatch = Regex.Match(mapUrl, @"^(-?\d{1,2}(?:\.\d+)?)[,\s]+(-?\d{1,3}(?:\.\d+)?)$");
            if (directMatch.Success &&
                decimal.TryParse(directMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal dLat) &&
                decimal.TryParse(directMatch.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal dLng) &&
                IsValid(dLat, dLng))
            {
                var point = new GeoPoint(dLat, dLng);
                _cache[mapUrl] = point;
                return point;
            }

            // 2. Direct extraction from input URL
            var directPoint = ExtractCoordinatesFromUrl(mapUrl);
            if (directPoint != null)
            {
                Console.WriteLine($"[GoogleMapLinkParser] ✅ Found in Input URL => Lat: {directPoint.Latitude}, Lng: {directPoint.Longitude}");
                _cache[mapUrl] = directPoint;
                return directPoint;
            }

            // 3. Follow short link redirect to extract full URL and place name
            try
            {
                Console.WriteLine($"[GoogleMapLinkParser] 2. Resolving short link...");
                var response = await _httpClient.GetAsync(mapUrl, ct);
                string finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? mapUrl;
                Console.WriteLine($"[GoogleMapLinkParser] 3. Resolved URL: {finalUrl}");

                var pointFromUrl = ExtractCoordinatesFromUrl(finalUrl);
                if (pointFromUrl != null)
                {
                    Console.WriteLine($"[GoogleMapLinkParser] ✅ Extracted from Resolved URL => Lat: {pointFromUrl.Latitude}, Lng: {pointFromUrl.Longitude}");
                    _cache[mapUrl] = pointFromUrl;
                    return pointFromUrl;
                }

                // 4. Extract place/address name from the Google Maps URL
                var placeAddress = ExtractPlaceAddressFromUrl(finalUrl);
                if (!string.IsNullOrWhiteSpace(placeAddress))
                {
                    Console.WriteLine($"[GoogleMapLinkParser] 4. Found Place Address: {placeAddress}");

                    // Geocode via LocationIQ API
                    if (!string.IsNullOrWhiteSpace(LocationIQApiKey))
                    {
                        var locationIqGeo = await GeocodeWithLocationIQAsync(placeAddress);
                        if (locationIqGeo != null)
                        {
                            Console.WriteLine($"[GoogleMapLinkParser] ✅ Exact Location (LocationIQ) => Lat: {locationIqGeo.Latitude}, Lng: {locationIqGeo.Longitude}");
                            _cache[mapUrl] = locationIqGeo;
                            return locationIqGeo;
                        }
                    }

                    // Geocode via Google API (if configured)
                    if (!string.IsNullOrWhiteSpace(GoogleApiKey))
                    {
                        var googleGeo = await GeocodeWithGoogleAsync(placeAddress);
                        if (googleGeo != null)
                        {
                            Console.WriteLine($"[GoogleMapLinkParser] ✅ Exact Location (Google API) => Lat: {googleGeo.Latitude}, Lng: {googleGeo.Longitude}");
                            _cache[mapUrl] = googleGeo;
                            return googleGeo;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleMapLinkParser] ❌ Exception: {ex.Message}");
            }

            // If coordinates cannot be determined with certainty, return NULL
            Console.WriteLine("[GoogleMapLinkParser] ⚠️ No coordinates found. Returning NULL (Visit GPS will be used).");
            Console.WriteLine($"==================== [GoogleMapLinkParser END] ====================\n");
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

        private static GeoPoint? ExtractCoordinatesFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string decoded = Uri.UnescapeDataString(url);

            // 1. Match explicit Place Marker !3d{lat}!4d{lng}
            var match3d = Regex.Match(decoded, @"!3d(-?\d{1,2}(?:\.\d+)+)!4d(-?\d{1,3}(?:\.\d+)+)");
            if (match3d.Success &&
                decimal.TryParse(match3d.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lat3d) &&
                decimal.TryParse(match3d.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lng3d) &&
                IsValid(lat3d, lng3d))
            {
                return new GeoPoint(lat3d, lng3d);
            }

            // 2. Match Query coordinates: ?q=lat,lng or ?ll=lat,lng or ?daddr=lat,lng
            var matchQ = Regex.Match(decoded, @"(?:[?&](?:q|query|ll|daddr)=)(-?\d{1,2}(?:\.\d+)+)[,\s%2C]+(-?\d{1,3}(?:\.\d+)+)");
            if (matchQ.Success &&
                decimal.TryParse(matchQ.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal latQ) &&
                decimal.TryParse(matchQ.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lngQ) &&
                IsValid(latQ, lngQ))
            {
                return new GeoPoint(latQ, lngQ);
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
                rawAddress, // 1. Try full address
                string.Join(", ", cleanParts) // 2. Try cleaned address
            };

            // 3. Try shop name + city + state/pin
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

        private static async Task<GeoPoint?> GeocodeWithGoogleAsync(string address)
        {
            try
            {
                string url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={GoogleApiKey}";
                var res = await _httpClient.GetAsync(url);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var results = doc.RootElement.GetProperty("results");
                    if (results.GetArrayLength() > 0)
                    {
                        var location = results[0].GetProperty("geometry").GetProperty("location");
                        decimal lat = location.GetProperty("lat").GetDecimal();
                        decimal lng = location.GetProperty("lng").GetDecimal();
                        if (IsValid(lat, lng)) return new GeoPoint(lat, lng);
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool IsValid(decimal lat, decimal lng)
        {
            return lat >= 8.0m && lat <= 37.5m && lng >= 68.0m && lng <= 97.5m;
        }
    }
}
