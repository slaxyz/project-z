using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ProjectZ.Combat;
using ProjectZ.Run;
using UnityEditor;
using UnityEngine;

namespace ProjectZ.EditorTools
{
    public static class CharacterExcelImporter
    {
        private const string DefaultExcelPath = @"C:\Users\Clement\Downloads\SOEdatas - Characters.xlsx";
        private const string DefaultExportFolder = @"C:\Users\Clement\Downloads\export";
        private const string ChampionCatalogPath = "Assets/ScriptableObjects/Run/ChampionCatalog.asset";
        private const string TaxonomyRoot = "Assets/ScriptableObjects/Characters/Taxonomy";
        private const string HeroArtRoot = "Assets/Resources/Art/Characters";

        private static readonly Dictionary<int, string> RarityNames = new Dictionary<int, string>
        {
            { 0, "Default" },
            { 1, "R" },
            { 2, "SR" },
            { 3, "SSR" },
            { 4, "LR" },
            { 5, "LR" }
        };

        private static readonly Dictionary<int, Color> RarityColors = new Dictionary<int, Color>
        {
            { 0, new Color(0.62f, 0.62f, 0.66f, 1f) },
            { 1, new Color(0.44f, 0.54f, 0.67f, 1f) },
            { 2, new Color(0.34f, 0.70f, 0.53f, 1f) },
            { 3, new Color(0.67f, 0.45f, 0.86f, 1f) },
            { 4, new Color(0.95f, 0.66f, 0.24f, 1f) },
            { 5, new Color(0.95f, 0.66f, 0.24f, 1f) }
        };

        private static readonly Dictionary<int, string> TypeNames = new Dictionary<int, string>
        {
            { 1, "Fire" },
            { 2, "Nature" },
            { 3, "Poison" },
            { 4, "Water" },
            { 5, "Normal" },
            { 6, "Mystic" }
        };

        private static readonly Dictionary<int, string> ClassNames = new Dictionary<int, string>
        {
            { 1, "Warrior" },
            { 2, "Tank" },
            { 3, "Rogue" },
            { 4, "Healer" },
            { 5, "Specialist" },
            { 6, "Gunner" }
        };

        [MenuItem("Tools/Project Z/Import Users")]
        public static void ImportFromExcel()
        {
            var excelPath = EditorUtility.OpenFilePanel("Choose Character Excel", Path.GetDirectoryName(DefaultExcelPath), "xlsx");
            if (string.IsNullOrWhiteSpace(excelPath))
            {
                return;
            }

            var exportFolder = EditorUtility.OpenFolderPanel("Choose Hero Image Export Folder", DefaultExportFolder, string.Empty);
            if (string.IsNullOrWhiteSpace(exportFolder))
            {
                return;
            }

            ImportFromPaths(excelPath, exportFolder);
        }

        public static void ImportFromDefaultPaths()
        {
            ImportFromPaths(DefaultExcelPath, DefaultExportFolder);
        }

        private static void ImportFromPaths(string excelPath, string exportFolder)
        {
            if (!File.Exists(excelPath))
            {
                Debug.LogError("Character import failed: excel not found at " + excelPath);
                return;
            }

            if (!Directory.Exists(exportFolder))
            {
                Debug.LogError("Character import failed: export folder not found at " + exportFolder);
                return;
            }

            try
            {
                var rows = ExcelReader.ReadRows(excelPath).ToList();
                if (rows.Count == 0)
                {
                    Debug.LogWarning("Character import aborted: no rows found.");
                    return;
                }

                EnsureFolder("Assets/ScriptableObjects");
                EnsureFolder("Assets/ScriptableObjects/Characters");
                EnsureFolder(TaxonomyRoot);
                EnsureFolder(TaxonomyRoot + "/Rarities");
                EnsureFolder(TaxonomyRoot + "/Types");
                EnsureFolder(TaxonomyRoot + "/Classes");
                EnsureFolder(TaxonomyRoot + "/Passives");
                EnsureFolder(HeroArtRoot);
                AssetDatabase.Refresh();

                var rarityById = new Dictionary<string, HeroRarityDefinitionAsset>();
                var typeById = new Dictionary<string, HeroTypeDefinitionAsset>();
                var classById = new Dictionary<string, HeroClassDefinitionAsset>();
                var passiveById = new Dictionary<string, HeroPassiveDefinitionAsset>();
                var champions = new List<ChampionDefinitionAsset>();

                EnsureBaselineTaxonomy(rarityById, typeById, classById);

                foreach (var row in rows)
                {
                    var nickname = row.Get("nickname");
                    if (string.IsNullOrWhiteSpace(nickname))
                    {
                        continue;
                    }

                    var championId = Slug(nickname);
                    var numericId = row.GetInt("id");
                    var rarityNum = row.GetInt("rarity");
                    var typeNum = row.GetInt("type");
                    var roleNum = row.GetInt("role");

                    var rarityId = "rarity_" + rarityNum;
                    var typeId = "type_" + typeNum;
                    var className = row.Get("className");
                    var classText = row.Get("classText");
                    var passiveId = "passive_" + championId;

                    var rarity = GetOrCreateRarity(rarityById, rarityId, rarityNum);
                    var typeLabel = TypeNames.TryGetValue(typeNum, out var tn) ? tn : "Type " + typeNum;
                    var classLabel = ClassNames.TryGetValue(roleNum, out var rn) ? rn : "Class " + roleNum;
                    var effectiveClassName = string.IsNullOrWhiteSpace(className) ? classLabel : className;
                    var classId = "class_" + Slug(effectiveClassName);
                    var type = GetOrCreateSimple(typeById, typeId, typeLabel, TaxonomyRoot + "/Types", x => ScriptableObject.CreateInstance<HeroTypeDefinitionAsset>());
                    var heroClass = GetOrCreateClass(classById, classId, effectiveClassName, classText);
                    var passive = GetOrCreatePassive(passiveById, passiveId, "Passive " + row.Get("nickname"), row.Get("passiveText"));

                    var avatarSprite = CopyAndLoadSprite(exportFolder, row.Get("nickname"), "Small", championId, "avatar", 1024);
                    var splashSprite = CopyAndLoadSprite(exportFolder, row.Get("nickname"), "Full", championId, "splash", 4096);

                    var champion = new ChampionDefinitionAsset(
                        numericId,
                        championId,
                        row.Get("nickname"),
                        row.Get("nickname"),
                        row.Get("name"),
                        row.Get("description"),
                        rarity,
                        type,
                        heroClass,
                        passive,
                        heroClass.DisplayName,
                        TierFromRarity(rarityNum),
                        MapElement(typeNum),
                        MapChampionClass(effectiveClassName),
                        UnlockCostFromRarity(rarityNum),
                        row.Get("description"),
                        row.GetInt("HP"),
                        row.GetInt("atk"),
                        row.GetInt("defense"),
                        row.GetInt("spe"),
                        avatarSprite,
                        splashSprite);
                    champions.Add(champion);
                }

                var catalog = AssetDatabase.LoadAssetAtPath<ChampionCatalogAsset>(ChampionCatalogPath);
                if (catalog == null)
                {
                    EnsureFolder("Assets/ScriptableObjects");
                    EnsureFolder("Assets/ScriptableObjects/Run");
                    catalog = ScriptableObject.CreateInstance<ChampionCatalogAsset>();
                    AssetDatabase.CreateAsset(catalog, ChampionCatalogPath);
                }

                catalog.ReplaceChampions(champions);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Character import done: " + champions.Count + " heroes.");
            }
            catch (Exception ex)
            {
                Debug.LogError("Character import failed: " + ex.Message);
            }
        }

        private static HeroRarityDefinitionAsset GetOrCreateRarity(Dictionary<string, HeroRarityDefinitionAsset> cache, string id, int rarityNum)
        {
            if (cache.TryGetValue(id, out var existing))
            {
                return existing;
            }

            var path = TaxonomyRoot + "/Rarities/" + id + ".asset";
            var rarity = AssetDatabase.LoadAssetAtPath<HeroRarityDefinitionAsset>(path);
            if (rarity == null)
            {
                rarity = ScriptableObject.CreateInstance<HeroRarityDefinitionAsset>();
                AssetDatabase.CreateAsset(rarity, path);
            }

            var so = new SerializedObject(rarity);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = RarityNames.TryGetValue(rarityNum, out var rarityName) ? rarityName : "Rarity " + rarityNum;
            so.FindProperty("backgroundColor").colorValue = RarityColors.TryGetValue(rarityNum, out var color) ? color : new Color(0.24f, 0.28f, 0.34f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rarity);
            cache[id] = rarity;
            return rarity;
        }

        private static TAsset GetOrCreateSimple<TAsset>(
            Dictionary<string, TAsset> cache,
            string id,
            string displayName,
            string rootPath,
            Func<string, TAsset> createFn) where TAsset : HeroKeyedDefinitionAsset
        {
            if (cache.TryGetValue(id, out var existing))
            {
                return existing;
            }

            var path = rootPath + "/" + id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
            if (asset == null)
            {
                asset = createFn(id);
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            cache[id] = asset;
            return asset;
        }

        private static HeroClassDefinitionAsset GetOrCreateClass(
            Dictionary<string, HeroClassDefinitionAsset> cache,
            string id,
            string className,
            string classDescription)
        {
            if (cache.TryGetValue(id, out var existing))
            {
                return existing;
            }

            var displayName = string.IsNullOrWhiteSpace(className) ? "Class" : className.Trim();
            var path = TaxonomyRoot + "/Classes/" + id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<HeroClassDefinitionAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HeroClassDefinitionAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = classDescription ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            cache[id] = asset;
            return asset;
        }

        private static HeroPassiveDefinitionAsset GetOrCreatePassive(
            Dictionary<string, HeroPassiveDefinitionAsset> cache,
            string id,
            string displayName,
            string description)
        {
            if (cache.TryGetValue(id, out var existing))
            {
                return existing;
            }

            var path = TaxonomyRoot + "/Passives/" + id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<HeroPassiveDefinitionAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HeroPassiveDefinitionAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            cache[id] = asset;
            return asset;
        }

        private static void EnsureBaselineTaxonomy(
            Dictionary<string, HeroRarityDefinitionAsset> rarityById,
            Dictionary<string, HeroTypeDefinitionAsset> typeById,
            Dictionary<string, HeroClassDefinitionAsset> classById)
        {
            foreach (var pair in RarityNames)
            {
                GetOrCreateRarity(rarityById, "rarity_" + pair.Key, pair.Key);
            }

            foreach (var pair in TypeNames)
            {
                GetOrCreateSimple(typeById, "type_" + pair.Key, pair.Value, TaxonomyRoot + "/Types", x => ScriptableObject.CreateInstance<HeroTypeDefinitionAsset>());
            }

            foreach (var pair in ClassNames)
            {
                GetOrCreateClass(classById, "class_" + Slug(pair.Value), pair.Value, string.Empty);
            }
        }

        private static Sprite CopyAndLoadSprite(string exportFolder, string nickname, string sizeLabel, string championId, string kind, int maxSize)
        {
            var source = Path.Combine(exportFolder, "Property 1=" + nickname + ", Size=" + sizeLabel + ".png");
            if (!File.Exists(source))
            {
                return null;
            }

            var targetFolder = HeroArtRoot + "/" + championId;
            EnsureFolder(targetFolder);
            var targetPath = targetFolder + "/" + championId + "_" + kind + ".png";
            File.Copy(source, ToAbsolutePath(targetPath), true);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            ConfigureSpriteImporter(targetPath, maxSize);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Sprite>(targetPath);
        }

        private static void ConfigureSpriteImporter(string assetPath, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = Mathf.Clamp(maxSize, 256, 8192);
            importer.SaveAndReimport();
        }

        private static string Slug(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "unknown";
            }

            var lower = text.Trim().ToLowerInvariant();
            lower = Regex.Replace(lower, "[^a-z0-9]+", "_");
            lower = lower.Trim('_');
            return string.IsNullOrWhiteSpace(lower) ? "unknown" : lower;
        }

        private static int TierFromRarity(int rarity)
        {
            var normalized = Mathf.Clamp(rarity, 0, 5);
            if (normalized <= 1) return 3;
            if (normalized == 2) return 4;
            if (normalized == 3) return 5;
            return 6;
        }

        private static int UnlockCostFromRarity(int rarity)
        {
            return Mathf.Clamp(rarity, 1, 6) * 25;
        }

        private static ElementType MapElement(int typeId)
        {
            switch (typeId)
            {
                case 1:
                    return ElementType.Fire;
                case 2:
                    return ElementType.Earth;
                case 3:
                    return ElementType.Air;
                case 4:
                    return ElementType.Water;
                case 5:
                    return ElementType.Earth;
                case 6:
                    return ElementType.Water;
                default:
                    return ElementType.Fire;
            }
        }

        private static ChampionClassType MapChampionClass(string className)
        {
            var normalized = (className ?? string.Empty).Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "warrior":
                case "tank":
                    return ChampionClassType.Vanguard;
                case "gunner":
                case "rogue":
                    return ChampionClassType.Striker;
                case "specialist":
                    return ChampionClassType.Controller;
                case "healer":
                    return ChampionClassType.Support;
                default:
                    return ChampionClassType.Vanguard;
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private sealed class RowData
        {
            private readonly Dictionary<string, string> _values;

            public RowData(Dictionary<string, string> values)
            {
                _values = values;
            }

            public string Get(string key)
            {
                if (!_values.TryGetValue(key, out var value))
                {
                    return string.Empty;
                }

                return value?.Trim() ?? string.Empty;
            }

            public int GetInt(string key)
            {
                var raw = Get(key);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return 0;
                }

                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                {
                    return i;
                }

                if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    return Mathf.RoundToInt(f);
                }

                return 0;
            }
        }

        private static class ExcelReader
        {
            public static IEnumerable<RowData> ReadRows(string xlsxPath)
            {
                using (var zip = ZipFile.OpenRead(xlsxPath))
                {
                    var sharedStrings = ReadSharedStrings(zip);
                    var sheetDoc = XDocument.Parse(ReadZipText(zip, "xl/worksheets/sheet1.xml"));
                    var ns = sheetDoc.Root?.Name.Namespace ?? XNamespace.None;

                    var rows = sheetDoc.Descendants(ns + "sheetData").Descendants(ns + "row").ToList();
                    if (rows.Count == 0)
                    {
                        yield break;
                    }

                    var headers = ReadRow(rows[0], ns, sharedStrings);
                    var maxCol = headers.Count == 0 ? 0 : headers.Keys.Max();
                    if (maxCol <= 0)
                    {
                        yield break;
                    }

                    for (var i = 1; i < rows.Count; i++)
                    {
                        var values = ReadRow(rows[i], ns, sharedStrings);
                        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        for (var col = 1; col <= maxCol; col++)
                        {
                            if (!headers.TryGetValue(col, out var header) || string.IsNullOrWhiteSpace(header))
                            {
                                continue;
                            }

                            dict[header.Trim()] = values.TryGetValue(col, out var cell) ? cell : string.Empty;
                        }

                        if (dict.Count > 0)
                        {
                            yield return new RowData(dict);
                        }
                    }
                }
            }

            private static Dictionary<int, string> ReadRow(XElement row, XNamespace ns, IReadOnlyList<string> shared)
            {
                var result = new Dictionary<int, string>();
                foreach (var cell in row.Elements(ns + "c"))
                {
                    var r = (string)cell.Attribute("r");
                    var col = GetColumnIndex(r);
                    if (col <= 0)
                    {
                        continue;
                    }

                    var type = (string)cell.Attribute("t");
                    var v = cell.Element(ns + "v")?.Value ?? string.Empty;
                    var text = string.Empty;
                    if (type == "s")
                    {
                        if (int.TryParse(v, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < shared.Count)
                        {
                            text = shared[sharedIndex];
                        }
                    }
                    else
                    {
                        text = v;
                    }

                    result[col] = text;
                }

                return result;
            }

            private static int GetColumnIndex(string cellRef)
            {
                if (string.IsNullOrWhiteSpace(cellRef))
                {
                    return 0;
                }

                var letters = new string(cellRef.TakeWhile(char.IsLetter).ToArray());
                var index = 0;
                for (var i = 0; i < letters.Length; i++)
                {
                    index = index * 26 + (letters[i] - 'A' + 1);
                }

                return index;
            }

            private static List<string> ReadSharedStrings(ZipArchive zip)
            {
                var text = ReadZipText(zip, "xl/sharedStrings.xml");
                if (string.IsNullOrWhiteSpace(text))
                {
                    return new List<string>();
                }

                var doc = XDocument.Parse(text);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
                return doc.Descendants(ns + "si")
                    .Select(si =>
                    {
                        var t = si.Element(ns + "t");
                        if (t != null)
                        {
                            return t.Value;
                        }

                        return string.Concat(si.Descendants(ns + "r").Select(r => r.Element(ns + "t")?.Value ?? string.Empty));
                    })
                    .ToList();
            }

            private static string ReadZipText(ZipArchive zip, string entryName)
            {
                var entry = zip.GetEntry(entryName);
                if (entry == null)
                {
                    return string.Empty;
                }

                using (var stream = entry.Open())
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
