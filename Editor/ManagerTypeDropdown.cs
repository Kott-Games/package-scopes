using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Kott.Scopes
{
    /// <summary>Searchable picker over all concrete, serializable <see cref="Manager" /> types, grouped by namespace.</summary>
    internal sealed class ManagerTypeDropdown : AdvancedDropdown
    {
        private readonly Action<Type> _onSelected;

        public ManagerTypeDropdown(AdvancedDropdownState state, Action<Type> onSelected) : base(state)
        {
            _onSelected = onSelected;
            minimumSize = new Vector2(280f, 320f);
        }

        /// <summary>Manager types that can be added to a scope, sorted by full name.</summary>
        internal static List<Type> GetManagerTypes()
        {
            List<Type> types = new();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<Manager>())
            {
                if (!type.IsAbstract && !type.ContainsGenericParameters && type.IsSerializable &&
                    type.GetConstructor(Type.EmptyTypes) != null)
                {
                    types.Add(type);
                }
            }

            types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            return types;
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new("Managers");
            Dictionary<string, AdvancedDropdownItem> groups = new();

            foreach (Type type in GetManagerTypes())
            {
                GetGroup(root, groups, type.Namespace).AddChild(new TypeItem(type));
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is TypeItem typeItem)
            {
                _onSelected(typeItem.Type);
            }
        }

        private static AdvancedDropdownItem GetGroup(AdvancedDropdownItem root,
            Dictionary<string, AdvancedDropdownItem> groups, string ns)
        {
            if (string.IsNullOrEmpty(ns))
            {
                return root;
            }

            if (groups.TryGetValue(ns, out AdvancedDropdownItem group))
            {
                return group;
            }

            int separator = ns.LastIndexOf('.');
            AdvancedDropdownItem parent =
                separator < 0 ? root : GetGroup(root, groups, ns.Substring(0, separator));
            group = new AdvancedDropdownItem(ns.Substring(separator + 1));
            parent.AddChild(group);
            groups.Add(ns, group);
            return group;
        }

        private sealed class TypeItem : AdvancedDropdownItem
        {
            public TypeItem(Type type) : base(ObjectNames.NicifyVariableName(type.Name))
            {
                Type = type;
            }

            public Type Type { get; }
        }
    }
}
