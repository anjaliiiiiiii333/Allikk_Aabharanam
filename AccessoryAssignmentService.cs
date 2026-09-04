using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopKeychainApp
{
    public sealed class AccessoryAsset
    {
        public string AccessoryId { get; set; }
        public string AccessoryName { get; set; }
        public string AssetPath { get; set; }
        public string Category { get; set; }
        public bool Enabled { get; set; }

        public override string ToString() => AccessoryName;
    }

    public sealed class AccessoryAssignment
    {
        public string Accessory { get; set; }
        public string Mode { get; set; }
    }

    public sealed class AccessoryAssignmentService
    {
        private readonly string _assignmentPath;
        private readonly Dictionary<string, AccessoryAssignment> _assignments;

        public AccessoryAssignmentService()
        {
            _assignmentPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DesktopKeychainApp",
                "accessory-assignments.json");
            _assignments = LoadAssignments();
        }

        public IReadOnlyList<AccessoryAsset> LoadAvailableAccessories()
        {
            string datasetRoot = FindDatasetRoot();
            if (datasetRoot == null)
                return Array.Empty<AccessoryAsset>();

            var accessories = new List<AccessoryAsset>();
            var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string projectRoot = Directory.GetParent(Directory.GetParent(datasetRoot).FullName).FullName;
            string assetsRoot = FindAssetsRoot(projectRoot, datasetRoot);
            string metadataPath = Path.Combine(datasetRoot, "accessories.json");

            if (File.Exists(metadataPath))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(metadataPath));
                if (document.RootElement.TryGetProperty("accessories", out JsonElement entries))
                {
                    foreach (JsonElement entry in entries.EnumerateArray())
                    {
                        string assetPath = entry.TryGetProperty("asset_path", out JsonElement path)
                            ? path.GetString()
                            : null;
                        string resolvedPath = ResolveAssetPath(assetsRoot, datasetRoot, assetPath);
                        if (resolvedPath == null || !knownPaths.Add(resolvedPath))
                            continue;

                        accessories.Add(new AccessoryAsset
                        {
                            AccessoryId = GetString(entry, "id") ?? Path.GetFileNameWithoutExtension(resolvedPath),
                            AccessoryName = GetString(entry, "name") ?? CreateDisplayName(resolvedPath),
                            AssetPath = resolvedPath,
                            Category = GetString(entry, "category") ?? "accessory",
                            Enabled = true
                        });
                    }
                }
            }

            foreach (string imagePath in DiscoverImageFiles(assetsRoot))
            {
                if (!knownPaths.Add(imagePath))
                    continue;

                accessories.Add(new AccessoryAsset
                {
                    AccessoryId = Path.GetFileNameWithoutExtension(imagePath),
                    AccessoryName = CreateDisplayName(imagePath),
                    AssetPath = imagePath,
                    Category = InferCategory(imagePath),
                    Enabled = true
                });
            }

            return accessories.Where(item => item.Enabled).ToList();
        }

        public AccessoryAssignment GetAssignment(string iconName)
        {
            return iconName != null && _assignments.TryGetValue(iconName, out AccessoryAssignment assignment)
                ? assignment
                : null;
        }

        public void SetManualAssignment(string iconName, AccessoryAsset accessory)
        {
            if (string.IsNullOrWhiteSpace(iconName) || accessory == null)
                return;

            _assignments[iconName] = new AccessoryAssignment
            {
                Accessory = accessory.AccessoryId,
                Mode = "manual"
            };
            SaveAssignments();
        }

        public void RemoveAssignment(string iconName)
        {
            if (iconName != null && _assignments.Remove(iconName))
                SaveAssignments();
        }

        public void GenerateAutomaticAssignments(IEnumerable<DesktopItem> desktopItems, IReadOnlyList<AccessoryAsset> accessories)
        {
            List<DesktopItem> items = desktopItems?.ToList() ?? new List<DesktopItem>();
            List<AccessoryAsset> pool = accessories?.Where(item => item.Enabled).ToList() ?? new List<AccessoryAsset>();
            if (pool.Count == 0)
                return;

            for (int index = pool.Count - 1; index > 0; index--)
            {
                int swapIndex = Random.Shared.Next(index + 1);
                (pool[index], pool[swapIndex]) = (pool[swapIndex], pool[index]);
            }

            int automaticIndex = 0;
            foreach (DesktopItem item in items)
            {
                if (_assignments.TryGetValue(item.Name, out AccessoryAssignment existing) &&
                    string.Equals(existing.Mode, "manual", StringComparison.OrdinalIgnoreCase))
                    continue;

                AccessoryAsset selected = pool[automaticIndex % pool.Count];
                automaticIndex++;
                _assignments[item.Name] = new AccessoryAssignment
                {
                    Accessory = selected.AccessoryId,
                    Mode = "automatic"
                };
            }

            SaveAssignments();
        }

        public AccessoryAsset FindAccessory(string accessoryId, IReadOnlyList<AccessoryAsset> accessories)
        {
            return accessories?.FirstOrDefault(item =>
                string.Equals(item.AccessoryId, accessoryId, StringComparison.OrdinalIgnoreCase));
        }

        private Dictionary<string, AccessoryAssignment> LoadAssignments()
        {
            try
            {
                if (!File.Exists(_assignmentPath))
                    return new Dictionary<string, AccessoryAssignment>(StringComparer.OrdinalIgnoreCase);

                var loaded = JsonSerializer.Deserialize<Dictionary<string, AccessoryAssignment>>(
                    File.ReadAllText(_assignmentPath));
                return loaded != null
                    ? new Dictionary<string, AccessoryAssignment>(loaded, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, AccessoryAssignment>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, AccessoryAssignment>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void SaveAssignments()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_assignmentPath));
            File.WriteAllText(_assignmentPath, JsonSerializer.Serialize(_assignments, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        private static string FindDatasetRoot()
        {
            var candidates = new List<string>();
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                candidates.Add(Path.Combine(directory.FullName, "datasets", "accessories"));
                candidates.Add(Path.Combine(directory.FullName, "OneDrive", "Documents", "Allikk_Aabharanam-master", "datasets", "accessories"));
                directory = directory.Parent;
            }

            return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "accessories.json")));
        }

        private static string ResolveAssetPath(string assetsRoot, string datasetRoot, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            string fileName = Path.GetFileName(assetPath);
            string exactPath = Path.Combine(datasetRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(exactPath))
                return Path.GetFullPath(exactPath);

            string[] candidates = Directory.GetFiles(assetsRoot, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase) ||
                               Path.GetFileName(path).StartsWith(Path.GetFileNameWithoutExtension(fileName) + "_", StringComparison.OrdinalIgnoreCase))
                .Where(IsImageFile)
                .ToArray();
            return candidates.FirstOrDefault();
        }

        private static IEnumerable<string> DiscoverImageFiles(string assetsRoot)
        {
            return Directory.Exists(assetsRoot)
                ? Directory.GetFiles(assetsRoot, "*", SearchOption.TopDirectoryOnly).Where(IsImageFile)
                : Enumerable.Empty<string>();
        }

        private static string FindAssetsRoot(string projectRoot, string datasetRoot)
        {
            string[] candidates =
            {
                Path.Combine(datasetRoot, "assets"),
                Path.Combine(projectRoot, "assets")
            };

            return candidates.FirstOrDefault(Directory.Exists) ?? Path.Combine(datasetRoot, "assets");
        }

        private static bool IsImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement value) ? value.GetString() : null;
        }

        private static string CreateDisplayName(string path)
        {
            return string.Join(" ", Path.GetFileNameWithoutExtension(path)
                .Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
        }

        private static string InferCategory(string path)
        {
            string name = Path.GetFileName(path).ToLowerInvariant();
            if (name.Contains("bow")) return "bow";
            if (name.Contains("mustache")) return "pin";
            return "keychain";
        }
    }
}