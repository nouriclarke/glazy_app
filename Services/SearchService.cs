using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ASTEM_DB.Services
{
    public record SearchMatch(string TileId, double Score);

    public class SearchService
    {
        private readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        private const string ApiBase = "http://localhost:8000";

        public async Task<List<SearchMatch>> SearchByImageAsync(string imagePath)
        {
            await using var stream = File.OpenRead(imagePath);
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(stream);

            // Let the server infer content type from the filename
            content.Add(streamContent, "file", Path.GetFileName(imagePath));

            var response = await _httpClient.PostAsync($"{ApiBase}/search/image", content);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetProperty("status").GetString() != "success")
                return new List<SearchMatch>();

            var matches = new List<SearchMatch>();
            foreach (var match in root.GetProperty("matches").EnumerateArray())
            {
                matches.Add(new SearchMatch(
                    match.GetProperty("tile_id").GetString()!,
                    match.GetProperty("score").GetDouble()
                ));
            }
            return matches;
        }
    }
}