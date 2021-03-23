using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.Commons.Editor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    public class ActionIdField : PopupField<ActionId> {
        private ActionGraphGlobalSettings settings;
        public ActionIdField() : this(null) { }
        public ActionIdField(string label) : base(label, GetItems(), 0) {
            settings = ProjectUtility.GetOrCreateProjectSettings<ActionGraphGlobalSettings>();
            this.formatListItemCallback = FormatCell;
            this.formatSelectedValueCallback = FormatCell;
        }

        private string FormatCell(ActionId actionId) {
            if (settings == null)
                return ActionId.UnknownActionName;
            if (!actionId.IsCreated)
                return ActionId.NullActionName;
            if (settings.TryGetActionInfo(actionId, out var info)) {
                return info.Name;
            }
            else {
                return ActionId.UnknownActionName;
            }
        }
        private static List<ActionId> GetItems() {
            var items = ProjectUtility.GetOrCreateProjectSettings<ActionGraphGlobalSettings>().Select(info =>
            {
                Debug.Log(info);
                return info.id;
            }).ToList();
            items.Insert(0, default);
            return items;
        }

        public new class UxmlFactory : UxmlFactory<ActionIdField, UxmlTraits> { }
        public new class UxmlTraits : PopupField<ActionId>.UxmlTraits { }
    }
}