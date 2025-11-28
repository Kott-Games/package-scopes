using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kott.Scopes
{
    [CustomPropertyDrawer(typeof(Manager), true)]
    public class ManagerPropertyDrawer : PropertyDrawer
    {
        private Type[] _managerTypes;
        private string[] _managerTypeNames;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;

            if (property.boxedValue is not Manager manager) return height;

            height += EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUIUtility.singleLineHeight;

            SerializedProperty propertyIterator = property;
            int depth = propertyIterator.depth;
            while (propertyIterator.NextVisible(propertyIterator.depth <= depth) &&
                propertyIterator.depth > depth)
            {
                height += EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUI.GetPropertyHeight(propertyIterator);
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CacheTypes();

            Rect subPosition = position;

            subPosition.height = EditorGUIUtility.singleLineHeight;

            Type typeCurrent = property.boxedValue?.GetType();
            Type typeNext = DrawTypeDropdown(subPosition, typeCurrent);
            if (typeCurrent != typeNext)
            {
                property.boxedValue = typeNext != null ? Activator.CreateInstance(typeNext) : null;
                typeCurrent = typeNext;
            }

            if (property.boxedValue is not Manager manager) return;

            subPosition.y += subPosition.height;
            subPosition.y += EditorGUIUtility.standardVerticalSpacing;
            subPosition.height = EditorGUIUtility.singleLineHeight;

            EditorGUI.LabelField(subPosition, typeCurrent.Name, EditorStyles.boldLabel);

            subPosition.y += subPosition.height;
            subPosition.y += EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty propertyIterator = property;
            int depth = propertyIterator.depth;
            while (propertyIterator.NextVisible(propertyIterator.depth <= depth) &&
                propertyIterator.depth > depth)
            {
                subPosition.height = EditorGUI.GetPropertyHeight(propertyIterator);

                EditorGUI.PropertyField(subPosition, propertyIterator, true);

                subPosition.y += subPosition.height;
                subPosition.y += EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private Type DrawTypeDropdown(Rect position, Type type)
        {
            int index = EditorGUI.Popup(position, "Create", -1, _managerTypeNames);
            if (index == 0) return null; // special case <none>
            if (index > 0 && index <= _managerTypeNames.Length) type = _managerTypes[index - 1];
            return type;
        }

        private void CacheTypes()
        {
            _managerTypes ??= TypeCache
                .GetTypesDerivedFrom<Manager>()
                .Where(type => !type.IsAbstract)
                .ToArray();

            _managerTypeNames ??= _managerTypes
                .Select(type => type.Name)
                .Prepend("<none>")
                .ToArray();
        }
    }
}
