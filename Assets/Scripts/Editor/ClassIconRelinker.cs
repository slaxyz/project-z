using System.Collections.Generic;
using ProjectZ.Run;
using UnityEditor;
using UnityEngine;

namespace ProjectZ.EditorTools
{
    public static class ClassIconRelinker
    {
        private static readonly Dictionary<string, string> IconPathByClassId = new Dictionary<string, string>
        {
            { "class_warrior", "Assets/Resources/Art/UI/ClassIcons/1.png" },
            { "class_tank", "Assets/Resources/Art/UI/ClassIcons/2.png" },
            { "class_rogue", "Assets/Resources/Art/UI/ClassIcons/3.png" },
            { "class_healer", "Assets/Resources/Art/UI/ClassIcons/4.png" },
            { "class_specialist", "Assets/Resources/Art/UI/ClassIcons/5.png" },
            { "class_gunner", "Assets/Resources/Art/UI/ClassIcons/6.png" }
        };

        [MenuItem("Project Z/Tools/Relink Class Icons")]
        private static void RelinkClassIcons()
        {
            var changed = 0;

            foreach (var entry in IconPathByClassId)
            {
                var classPath = "Assets/ScriptableObjects/Characters/Taxonomy/Classes/" + entry.Key + ".asset";
                var classAsset = AssetDatabase.LoadAssetAtPath<HeroClassDefinitionAsset>(classPath);
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(entry.Value);
                if (classAsset == null || icon == null)
                {
                    continue;
                }

                var so = new SerializedObject(classAsset);
                var iconProperty = so.FindProperty("icon");
                if (iconProperty == null || iconProperty.objectReferenceValue == icon)
                {
                    continue;
                }

                iconProperty.objectReferenceValue = icon;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(classAsset);
                changed++;
            }

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log("Class icon relink complete: " + changed + " asset(s) updated.");
        }
    }
}
