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

        // In-memory thread-safe cache to instantly serve multiple users
        private static readonly ConcurrentDictionary<string, GeoPoint> _cache = new();

        // Semaphore lock to prevent server IP blocking when multiple users click at once
        private static readonly SemaphoreSlim _throttler = new(1, 1);

        private static readonly (decimal Lat, decimal Lng)[] GenericFallbacks = new[]
        {
            (23.02388315m, 72.5086395m),
            (23.02260105m, 72.5086395m),
            (23.0225m, 72.5714m),
            (22.9965824m, 72.5024768m)
        };

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

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
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

            // 1. Direct coordinate format: "21.7583, 72.1458"
            var directMatch = Regex.Match(mapUrl, @"^(-?\d{1,2}(?:\.\d+)?)[,\s]+(-?\d{1,3}(?:\.\d+)?)$");
            if (directMatch.Success &&
                decimal.TryParse(directMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal dLat) &&
                decimal.TryParse(directMatch.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal dLng) &&
                IsValid(dLat, dLng))
            {
                Console.WriteLine($"[GoogleMapLinkParser] ✅ Direct Coordinates => Latitude: {dLat}, Longitude: {dLng}");
                Console.WriteLine($"==================== [GoogleMapLinkParser END] ====================\n");
                return new GeoPoint(dLat, dLng);
            }

            // 2. Direct extraction from input URL
            var directPoint = ExtractCoordinatesFromUrl(mapUrl);
            if (directPoint != null)
            {
                Console.WriteLine($"[GoogleMapLinkParser] ✅ Found in Input URL => Latitude: {directPoint.Latitude}, Longitude: {directPoint.Longitude}");
                Console.WriteLine($"==================== [GoogleMapLinkParser END] ====================\n");
                return directPoint;
            }

            // 3. Resolve short link by following redirects
            try
            {
                Console.WriteLine($"[GoogleMapLinkParser] 2. Resolving short link...");
                var response = await _httpClient.GetAsync(mapUrl, ct);
                string finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? mapUrl;
                Console.WriteLine($"[GoogleMapLinkParser] 3. Resolved URL: {finalUrl}");

                var point = ExtractCoordinatesFromUrl(finalUrl);
                if (point != null)
                {
                    Console.WriteLine($"[GoogleMapLinkParser] ✅ Extracted from URL => Latitude: {point.Latitude}, Longitude: {point.Longitude}");
                    Console.WriteLine($"==================== [GoogleMapLinkParser END] ====================\n");
                    return point;
                }

                var placeName = ExtractPlaceNameFromUrl(finalUrl);
                if (!string.IsNullOrWhiteSpace(placeName))
                {
                    Console.WriteLine($"[GoogleMapLinkParser] 4. Place: {placeName}");

                    // Check fast in-memory cache first
                    if (_cache.TryGetValue(placeName, out var cachedPoint))
                    {
                        Console.WriteLine($"[GoogleMapLinkParser] ⚡ Served from In-Memory Cache => Latitude: {cachedPoint.Latitude}, Longitude: {cachedPoint.Longitude}");
                        Console.WriteLine($"==================== [GoogleMapLinkParser END] ====================\n");
                        return cachedPoint;
                    }

                    var resolvedGeo = await GeocodePlaceAsync(placeName);
                    if (resolvedGeo != null)
                    {
                        _cache[placeName] = resolvedGeo; // Save to cache
                        Console.WriteLine($"[GoogleMapLinkParser] ✅ Exact Location => Latitude: {resolvedGeo.Latitude}, Longitude: {resolvedGeo.Longitude}");
                        Console.WriteLine($"==================== [GoogleMapLinkParser END] ====================\n");
                        return resolvedGeo;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleMapLinkParser] ❌ Exception: {ex.Message}");
            }

            Console.WriteLine("[GoogleMapLinkParser] ⚠️ No coordinates found. Returning null.");
            Console.WriteLine($"==================== [GoogleMapLinkParser END] ====================\n");
            return null;
        }

        private static string? ExtractPlaceNameFromUrl(string url)
        {
            var match = Regex.Match(url, @"/maps/place/([^/@?]+)");
            if (match.Success)
            {
                return Uri.UnescapeDataString(match.Groups[1].Value.Replace('+', ' '));
            }
            return null;
        }

        private static async Task<GeoPoint?> GeocodePlaceAsync(string rawPlace)
        {
            var pinMatch = Regex.Match(rawPlace, @"\b(\d{6})\b");
            string pincode = pinMatch.Success ? pinMatch.Groups[1].Value : "";

            var rawParts = rawPlace.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanParts = new List<string>();

            foreach (var p in rawParts)
            {
                string cleaned = p.Trim();
                cleaned = Regex.Replace(cleaned, @"^[A-Z0-9]{4,8}\+[A-Z0-9]{2,4}\s*", "", RegexOptions.IgnoreCase).Trim();
                cleaned = Regex.Replace(cleaned, @"\b(near|opp|opposite|behind|beside|at|shop no|plot no)\b\.?", "", RegexOptions.IgnoreCase).Trim();
                if (!int.TryParse(cleaned, out _) && !string.IsNullOrWhiteSpace(cleaned) && cleaned.Length >= 2)
                    cleanParts.Add(cleaned);
            }

            var queries = new List<string>();
            string shopName = cleanParts.Count > 0 ? cleanParts[0] : "";
            string stateOrPin = cleanParts.Count > 0 ? cleanParts[cleanParts.Count - 1] : "";
            string cityOrArea = cleanParts.Count >= 2 ? cleanParts[cleanParts.Count - 2] : "";
            string area = cleanParts.Count >= 3 ? cleanParts[cleanParts.Count - 3] : "";

            if (!string.IsNullOrWhiteSpace(shopName) && !string.IsNullOrWhiteSpace(cityOrArea))
                queries.Add($"{shopName}, {cityOrArea}");

            if (!string.IsNullOrWhiteSpace(area) && !string.IsNullOrWhiteSpace(cityOrArea))
                queries.Add($"{area}, {cityOrArea} {pincode}".Trim());

            if (!string.IsNullOrWhiteSpace(cityOrArea) && !string.IsNullOrWhiteSpace(stateOrPin))
                queries.Add($"{cityOrArea}, {stateOrPin}");

            if (!string.IsNullOrWhiteSpace(cityOrArea))
                queries.Add($"{cityOrArea}, India");

            if (!string.IsNullOrWhiteSpace(pincode))
                queries.Add($"{pincode}, India");

            queries.Add(string.Join(", ", cleanParts));

            // Thread safety lock for concurrent users
            await _throttler.WaitAsync();
            try
            {
                foreach (var q in queries)
                {
                    try
                    {
                        Console.WriteLine($"[GoogleMapLinkParser] Trying Geocode: {q}");
                        string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(q)}&countrycodes=in&format=json&limit=1";
                        using var req = new HttpRequestMessage(HttpMethod.Get, url);
                        req.Headers.Add("User-Agent", "StenfordAppBackend/1.0");

                        var res = await _httpClient.SendAsync(req);
                        if (res.IsSuccessStatusCode)
                        {
                            string json = await res.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                            {
                                var first = root[0];
                                if (first.TryGetProperty("lat", out var latP) && first.TryGetProperty("lon", out var lonP))
                                {
                                    if (decimal.TryParse(latP.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lat) &&
                                        decimal.TryParse(lonP.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lon))
                                    {
                                        if (IsValid(lat, lon))
                                            return new GeoPoint(lat, lon);
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    await Task.Delay(200); // Safe spacing to prevent server IP blocking
                }
            }
            finally
            {
                _throttler.Release();
            }

            return null;
        }

        private static GeoPoint? ExtractCoordinatesFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            string decoded = Uri.UnescapeDataString(url);

            var match3d = Regex.Match(decoded, @"!3d(-?\d{1,2}(?:\.\d+)+)!4d(-?\d{1,3}(?:\.\d+)+)");
            if (match3d.Success &&
                decimal.TryParse(match3d.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lat3d) &&
                decimal.TryParse(match3d.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lng3d) &&
                IsValid(lat3d, lng3d))
            {
                return new GeoPoint(lat3d, lng3d);
            }

            var matchQ = Regex.Match(decoded, @"(?:[?&](?:q|query)=)(-?\d{1,2}(?:\.\d+)+)[,\s]+(-?\d{1,3}(?:\.\d+)+)");
            if (matchQ.Success &&
                decimal.TryParse(matchQ.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal latQ) &&
                decimal.TryParse(matchQ.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal lngQ) &&
                IsValid(latQ, lngQ))
            {
                return new GeoPoint(latQ, lngQ);
            }

            return null;
        }

        private static bool IsValid(decimal lat, decimal lng)
        {
            if (lat < 8.0m || lat > 37.5m || lng < 68.0m || lng > 97.5m)
                return false;

            foreach (var fallback in GenericFallbacks)
            {
                if (Math.Abs(lat - fallback.Lat) < 0.0001m && Math.Abs(lng - fallback.Lng) < 0.0001m)
                    return false;
            }

            return true;
        }
    }
}
