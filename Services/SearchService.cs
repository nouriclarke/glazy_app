using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ASTEM_DB.Services
{
    public record SearchMatch(string TileId, double Score);

    public class SearchService
    {
        private const int ResultCount = 10; // adjust as needed
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);
        private readonly HttpClient _httpClient = new() { Timeout = RequestTimeout };

        private static string ApiBase =>
            (Environment.GetEnvironmentVariable("GLAZE_DAEMON_URL") ?? "http://localhost:8000").TrimEnd('/');

        public async Task<List<SearchMatch>> SearchByImageAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            await using var stream = File.OpenRead(imagePath);
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(stream);
            content.Add(streamContent, "file", Path.GetFileName(imagePath));

            using var response = await _httpClient.PostAsync($"{ApiBase}/search/image?n_results={ResultCount}", content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Image search server returned {(int)response.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : string.Empty;

            if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                var message = root.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : "Image search did not return matches.";
                throw new InvalidOperationException(message);
            }

            var matches = new List<SearchMatch>();
            if (!root.TryGetProperty("matches", out var matchesElement) || matchesElement.ValueKind != JsonValueKind.Array)
                return matches;

            foreach (var match in matchesElement.EnumerateArray())
            {
                var tileId = match.TryGetProperty("tile_id", out var tileIdElement) ? tileIdElement.GetString() : string.Empty;
                if (string.IsNullOrWhiteSpace(tileId)) continue;

                var score = match.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetDouble(out var parsedScore)
                    ? parsedScore : 0;
                matches.Add(new SearchMatch(tileId, score));
            }
            return matches;
        }
    }
}