using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AccessoryPrototype
{
    public sealed class DatasetAccessory
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public string AssetPath { get; init; }
        public string Category { get; init; }

        public override string ToString() => Name;
    }

    public static class DatasetCatalog
    {
        public static IReadOnlyList<DatasetAccessory> Load()
        {
            string datasetRoot = FindDatasetRoot();
            if (datasetRoot == null)
                return Array.Empty<DatasetAccessory>();

            string metadataPath = Path.Combine(datasetRoot, "accessories.json");
            if (!File.Exists(metadataPath))
                return Array.Empty<DatasetAccessory>();

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var result = new List<DatasetAccessory>();
            if (!document.RootElement.TryGetProperty("accessories", out JsonElement entries))
                return result;

            foreach (JsonElement entry in entries.EnumerateArray())
            {
                string relativeAsset = ReadString(entry, "asset_path");
                string fileName = Path.GetFileName(relativeAsset ?? string.Empty);
                string assetPath = FindExistingImage(datasetRoot, fileName);
                if (assetPath == null)
                    continue;

                result.Add(new DatasetAccessory
                {
                    Id = ReadString(entry, "id") ?? Path.GetFileNameWithoutExtension(assetPath),
                    Name = ReadString(entry, "name") ?? Path.GetFileNameWithoutExtension(assetPath),
                    Category = ReadString(entry, "category") ?? "accessory",
                    AssetPath = assetPath
                });
            }

            return result;
        }

        private static string FindDatasetRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string direct = Path.Combine(directory.FullName, "datasets", "accessories");
                if (File.Exists(Path.Combine(direct, "accessories.json")))
                    return direct;

                string nested = Path.Combine(
                    directory.FullName,
                    "OneDrive",
                    "Documents",
                    "Allikk_Aabharanam-master",
                    "datasets",
                    "accessories");
                if (File.Exists(Path.Combine(nested, "accessories.json")))
                    return nested;

                directory = directory.Parent;
            }

            return null;
        }

        private static string FindExistingImage(string datasetRoot, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            DirectoryInfo projectRoot = Directory.GetParent(Directory.GetParent(datasetRoot).FullName);
            return Directory.GetFiles(projectRoot.FullName, "*", SearchOption.AllDirectories)
                .Where(IsImage)
                .FirstOrDefault(path => string.Equals(
                    Path.GetFileName(path),
                    fileName,
                    StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileNameWithoutExtension(path).StartsWith(
                        Path.GetFileNameWithoutExtension(fileName) + "_",
                        StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsImage(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;
        }
    }
}
