using System.Linq;
using ProjectZ.Run;
using ProjectZ.UI;
using UnityEditor;
using UnityEngine;

namespace ProjectZ.Editor
{
    [CustomEditor(typeof(HeroSwitchPanelView))]
    public class HeroSwitchPanelViewEditor : UnityEditor.Editor
    {
        private SerializedProperty _slotsProperty;
        private SerializedProperty _useDebugSlotOverridesProperty;
        private SerializedProperty _debugChampionIdsProperty;
        private SerializedProperty _selectedSlotIndexProperty;

        private void OnEnable()
        {
            _slotsProperty = serializedObject.FindProperty("slots");
            _useDebugSlotOverridesProperty = serializedObject.FindProperty("useDebugSlotOverrides");
            _debugChampionIdsProperty = serializedObject.FindProperty("debugChampionIds");
            _selectedSlotIndexProperty = serializedObject.FindProperty("selectedSlotIndex");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_slotsProperty, true);
            EditorGUILayout.PropertyField(_useDebugSlotOverridesProperty);
            DrawSelectedSlotPopup();

            if (_useDebugSlotOverridesProperty.boolValue)
            {
                DrawChampionDropdowns();
            }

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Refresh Slots"))
            {
                foreach (var targetObject in targets.OfType<HeroSwitchPanelView>())
                {
                    targetObject.RefreshSlots();
                    EditorUtility.SetDirty(targetObject);
                }
            }
        }

        private void DrawSelectedSlotPopup()
        {
            var labels = new[] { "Slot 1", "Slot 2", "Slot 3" };
            _selectedSlotIndexProperty.intValue = EditorGUILayout.Popup(
                "Selected Slot",
                Mathf.Clamp(_selectedSlotIndexProperty.intValue, 0, labels.Length - 1),
                labels);
        }

        private void DrawChampionDropdowns()
        {
            EnsureArraySize(_debugChampionIdsProperty, 3);

            var champions = ChampionCatalog.AllAssets
                .Where(champion => champion != null && !string.IsNullOrWhiteSpace(champion.Id))
                .ToList();

            var options = new string[champions.Count + 1];
            options[0] = "None";
            for (var i = 0; i < champions.Count; i++)
            {
                options[i + 1] = champions[i].DisplayName + " (" + champions[i].Id + ")";
            }

            for (var i = 0; i < 3; i++)
            {
                var property = _debugChampionIdsProperty.GetArrayElementAtIndex(i);
                var currentId = property.stringValue;
                var selectedIndex = 0;

                for (var optionIndex = 0; optionIndex < champions.Count; optionIndex++)
                {
                    if (champions[optionIndex].Id == currentId)
                    {
                        selectedIndex = optionIndex + 1;
                        break;
                    }
                }

                var nextIndex = EditorGUILayout.Popup("Slot " + (i + 1) + " Hero", selectedIndex, options);
                property.stringValue = nextIndex <= 0 ? string.Empty : champions[nextIndex - 1].Id;
            }
        }

        private static void EnsureArraySize(SerializedProperty property, int size)
        {
            while (property.arraySize < size)
            {
                property.InsertArrayElementAtIndex(property.arraySize);
            }

            while (property.arraySize > size)
            {
                property.DeleteArrayElementAtIndex(property.arraySize - 1);
            }
        }
    }
}
