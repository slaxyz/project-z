using System.Collections.Generic;
using System.Linq;
using ProjectZ.Run;
using UnityEditor;
using UnityEngine;

namespace ProjectZ.EditorTools
{
    public static class ChampionCatalogAutoRelinker
    {
        private const string CatalogPath = "Assets/ScriptableObjects/Run/ChampionCatalog.asset";
        private const string TaxonomyRoot = "Assets/ScriptableObjects/Characters/Taxonomy";

        [InitializeOnLoadMethod]
        private static void EnsureRelinkedOnEditorLoad()
        {
            EditorApplication.delayCall += RelinkMissingReferences;
        }

        private static void RelinkMissingReferences()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ChampionCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                return;
            }

            var rarities = LoadById<HeroRarityDefinitionAsset>();
            var types = LoadById<HeroTypeDefinitionAsset>();
            var classes = LoadById<HeroClassDefinitionAsset>();
            var passives = LoadById<HeroPassiveDefinitionAsset>();

            var so = new SerializedObject(catalog);
            var champions = so.FindProperty("champions");
            if (champions == null || !champions.isArray)
            {
                return;
            }

            var changed = 0;
            for (var i = 0; i < champions.arraySize; i++)
            {
                var champion = champions.GetArrayElementAtIndex(i);
                var id = Normalize(champion.FindPropertyRelative("id")?.stringValue);
                var roleLabel = Normalize(champion.FindPropertyRelative("role")?.stringValue);
                var tier = champion.FindPropertyRelative("tierStars")?.intValue ?? 3;
                var element = champion.FindPropertyRelative("element")?.enumValueIndex ?? 0;

                changed += SetIfMissing(champion, "rarityDefinition", ResolveRarity(tier, rarities));
                changed += SetIfMissing(champion, "typeDefinition", ResolveType(id, element, types));
                changed += SetIfMissing(champion, "classDefinition", ResolveClass(roleLabel, classes));
                changed += SetIfMissing(champion, "passiveDefinition", ResolvePassive(id, passives));
            }

            if (changed <= 0)
            {
                return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Auto relink complete: fixed " + changed + " missing champion taxonomy link(s).");
        }

        private static int SetIfMissing<TAsset>(SerializedProperty champion, string fieldName, TAsset value) where TAsset : Object
        {
            var field = champion.FindPropertyRelative(fieldName);
            if (field == null || field.objectReferenceValue != null || value == null)
            {
                return 0;
            }

            field.objectReferenceValue = value;
            return 1;
        }

        private static Dictionary<string, TAsset> LoadById<TAsset>() where TAsset : HeroKeyedDefinitionAsset
        {
            var map = new Dictionary<string, TAsset>();
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { TaxonomyRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                var id = Normalize(asset.Id);
                if (string.IsNullOrWhiteSpace(id) || map.ContainsKey(id))
                {
                    continue;
                }

                map.Add(id, asset);
            }

            return map;
        }

        private static HeroRarityDefinitionAsset ResolveRarity(int tierStars, Dictionary<string, HeroRarityDefinitionAsset> map)
        {
            var index = Mathf.Clamp(tierStars - 2, 0, 4);
            map.TryGetValue("rarity_" + index, out var asset);
            return asset;
        }

        private static HeroTypeDefinitionAsset ResolveType(string championId, int element, Dictionary<string, HeroTypeDefinitionAsset> map)
        {
            if (map.TryGetValue(GetTypeIdByChampion(championId), out var byChampion))
            {
                return byChampion;
            }

            var fallback = element switch
            {
                0 => "type_1",
                1 => "type_3",
                2 => "type_2",
                3 => "type_4",
                _ => "type_1"
            };
            map.TryGetValue(fallback, out var byElement);
            return byElement;
        }

        private static HeroClassDefinitionAsset ResolveClass(string roleLabel, Dictionary<string, HeroClassDefinitionAsset> map)
        {
            map.TryGetValue("class_" + roleLabel, out var asset);
            return asset;
        }

        private static HeroPassiveDefinitionAsset ResolvePassive(string championId, Dictionary<string, HeroPassiveDefinitionAsset> map)
        {
            map.TryGetValue("passive_" + championId, out var asset);
            return asset;
        }

        private static string GetTypeIdByChampion(string championId)
        {
            return championId switch
            {
                "ace" => "type_1",
                "blaze" => "type_1",
                "crusher" => "type_2",
                "vortex" => "type_2",
                "phantom" => "type_3",
                "whiplash" => "type_3",
                "wrench" => "type_5",
                "slugger" => "type_5",
                "sonar" => "type_6",
                "psyche" => "type_6",
                _ => string.Empty
            };
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
