using ASTEM_DB.ViewModels;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
namespace ASTEM_DB.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString = "server=localhost;port=3306;user=ceramadmin;password=J9J9NasakeMuyouAsuteroidoBerutoNo;database=tilearchive;Charset=utf8mb4;";

        public async Task<List<string>> GetGlazeTypesAsync()
        {
            var glazeTypes = new List<string>();
            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();
                string query = "SELECT Name FROM glazetype";
                await using var cmd = new MySqlCommand(query, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    glazeTypes.Add(reader.GetString("Name"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect/query MariaDB: {ex.Message}");
            }
            return glazeTypes;
        }

        public async Task<List<string>> GetSurfaceCondition()
        {
            var conditions = new List<string>();
            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();
                string query = "SELECT Name FROM surfacecondition";
                await using var cmd = new MySqlCommand(query, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    conditions.Add(reader.GetString("Name"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect/query MariaDB: {ex.Message}");
            }
            return conditions;
        }

        public async Task<List<string>> GetFiringType()
        {
            var firingTypes = new List<string>();
            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();
                string query = "SELECT FiringType FROM testpiece GROUP BY FiringType";
                await using var cmd = new MySqlCommand(query, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    firingTypes.Add(reader.GetString("FiringType"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect/query MariaDB: {ex.Message}");
            }
            return firingTypes;
        }

        public async Task<List<CardItemViewModel>> GetFilteredCardItemsAsync(string? glazeType, string? surfaceCondition, string? firingType)
        {
            var items = new List<CardItemViewModel>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            var filters = new List<string>();
            if (!string.IsNullOrWhiteSpace(glazeType) && glazeType != "All")
                filters.Add("gt.Name = @GlazeType");
            if (!string.IsNullOrWhiteSpace(surfaceCondition) && surfaceCondition != "All")
                filters.Add("sc.Name = @SurfaceCondition");
            if (!string.IsNullOrWhiteSpace(firingType) && firingType != "All")
                filters.Add("tp.FiringType = @FiringType");

            string whereClause = filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : "";
            string query = $@"
                SELECT tp.ID, tp.Image, tp.Color_L, tp.Color_A, tp.Color_B,
                       tp.FiringType, tp.SoilType, tp.ChemicalComposition,
                       gt.Name AS GlazeType, sc.Name AS SurfaceCondition
                FROM testpiece tp
                LEFT JOIN glazetype gt ON tp.GlazeTypeID = gt.ID
                LEFT JOIN surfacecondition sc ON tp.SurfaceConditionID = sc.ID
                {whereClause};";

            await using var cmd = new MySqlCommand(query, conn);
            if (query.Contains("@GlazeType")) cmd.Parameters.AddWithValue("@GlazeType", glazeType);
            if (query.Contains("@SurfaceCondition")) cmd.Parameters.AddWithValue("@SurfaceCondition", surfaceCondition);
            if (query.Contains("@FiringType")) cmd.Parameters.AddWithValue("@FiringType", firingType);

            await using var reader = await cmd.ExecuteReaderAsync();
            string basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
            string placeholderPath = Path.Combine(basePath, "Assets/placeholder.png");

            while (await reader.ReadAsync())
            {
                byte[] imageBytes = (byte[])reader["Image"];
                using var memoryStream = new MemoryStream(imageBytes);
                items.Add(new CardItemViewModel
                {
                    Id = reader["ID"].ToString()!,
                    Image = new Bitmap(memoryStream),
                    GlazeType = reader["GlazeType"].ToString() ?? "Unknown",
                    SurfaceCondition = reader["SurfaceCondition"].ToString() ?? "Unknown",
                    ColorL = Convert.ToDouble(reader["Color_L"]),
                    ColorA = Convert.ToDouble(reader["Color_A"]),
                    ColorB = Convert.ToDouble(reader["Color_B"]),
                    Lab = $"{reader["Color_L"]}, {reader["Color_A"]}, {reader["Color_B"]}",
                    FiringType = reader["FiringType"].ToString() ?? "",
                    SoilType = reader["SoilType"].ToString() ?? "",
                    ChemicalComposition = reader["ChemicalComposition"].ToString() ?? ""
                });
            }
            return items;
        }

        public async Task<List<CardItemViewModel>> GetFilteredCardItemMetadataAsync(string? glazeType, string? surfaceCondition, string? firingType)
        {
            var items = new List<CardItemViewModel>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            var filters = new List<string>();
            if (!string.IsNullOrWhiteSpace(glazeType) && glazeType != "All")
                filters.Add("gt.Name = @GlazeType");
            if (!string.IsNullOrWhiteSpace(surfaceCondition) && surfaceCondition != "All")
                filters.Add("sc.Name = @SurfaceCondition");
            if (!string.IsNullOrWhiteSpace(firingType) && firingType != "All")
                filters.Add("tp.FiringType = @FiringType");

            string whereClause = filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : "";
            string query = $@"
                SELECT tp.ID, tp.Color_L, tp.Color_A, tp.Color_B,
                       tp.FiringType, tp.SoilType, tp.ChemicalComposition,
                       gt.Name AS GlazeType, sc.Name AS SurfaceCondition
                FROM testpiece tp
                LEFT JOIN glazetype gt ON tp.GlazeTypeID = gt.ID
                LEFT JOIN surfacecondition sc ON tp.SurfaceConditionID = sc.ID
                {whereClause};";

            await using var cmd = new MySqlCommand(query, conn);
            if (query.Contains("@GlazeType")) cmd.Parameters.AddWithValue("@GlazeType", glazeType);
            if (query.Contains("@SurfaceCondition")) cmd.Parameters.AddWithValue("@SurfaceCondition", surfaceCondition);
            if (query.Contains("@FiringType")) cmd.Parameters.AddWithValue("@FiringType", firingType);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new CardItemViewModel
                {
                    Id = reader["ID"].ToString()!,
                    GlazeType = reader["GlazeType"].ToString() ?? "Unknown",
                    SurfaceCondition = reader["SurfaceCondition"].ToString() ?? "Unknown",
                    ColorL = Convert.ToDouble(reader["Color_L"]),
                    ColorA = Convert.ToDouble(reader["Color_A"]),
                    ColorB = Convert.ToDouble(reader["Color_B"]),
                    Lab = $"{reader["Color_L"]}, {reader["Color_A"]}, {reader["Color_B"]}",
                    FiringType = reader["FiringType"].ToString() ?? "",
                    SoilType = reader["SoilType"].ToString() ?? "",
                    ChemicalComposition = reader["ChemicalComposition"].ToString() ?? ""
                });
            }
            return items;
        }

        public async Task<Bitmap?> GetImageByIdAsync(string id)
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            string query = "SELECT Image FROM testpiece WHERE ID = @Id LIMIT 1";
            await using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                if (reader["Image"] is byte[] imageBytes && imageBytes.Length > 0)
                {
                    using var memoryStream = new MemoryStream(imageBytes);
                    return new Bitmap(memoryStream);
                }
            }
            return null;
        }

        // Used by image search to load a full tile record by its ID
        public async Task<CardItemViewModel?> GetCardItemByIdAsync(string id)
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = @"
                SELECT tp.ID, tp.Image, tp.Color_L, tp.Color_A, tp.Color_B,
                       tp.FiringType, tp.SoilType, tp.ChemicalComposition,
                       gt.Name AS GlazeType, sc.Name AS SurfaceCondition
                FROM testpiece tp
                LEFT JOIN glazetype gt ON tp.GlazeTypeID = gt.ID
                LEFT JOIN surfacecondition sc ON tp.SurfaceConditionID = sc.ID
                WHERE tp.ID = @Id
                LIMIT 1";

            await using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                byte[] imageBytes = (byte[])reader["Image"];
                using var memoryStream = new MemoryStream(imageBytes);
                return new CardItemViewModel
                {
                    Id = reader["ID"].ToString()!,
                    Image = new Bitmap(memoryStream),
                    GlazeType = reader["GlazeType"].ToString() ?? "Unknown",
                    SurfaceCondition = reader["SurfaceCondition"].ToString() ?? "Unknown",
                    ColorL = Convert.ToDouble(reader["Color_L"]),
                    ColorA = Convert.ToDouble(reader["Color_A"]),
                    ColorB = Convert.ToDouble(reader["Color_B"]),
                    Lab = $"{reader["Color_L"]}, {reader["Color_A"]}, {reader["Color_B"]}",
                    FiringType = reader["FiringType"].ToString() ?? "",
                    SoilType = reader["SoilType"].ToString() ?? "",
                    ChemicalComposition = reader["ChemicalComposition"].ToString() ?? ""
                };
            }
            return null;
        }
    }
}