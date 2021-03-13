using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    [CustomPropertyDrawer(typeof(ActionType))]
    public class ActionTypePropertyDrawer : PropertyDrawer {
        

        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var asset = ProjectUtility.GetSettings<ActionGraphGlobalSettings>();
            //var actions = new List<string>();
            List<string> types = asset == null ? new List<string>() : asset.actions.Select(a => a.guid).ToList();
            types.Insert(0, string.Empty);
            var popup = new PopupField<string>(property.displayName, types, property.FindPropertyRelative("guid").stringValue, (choice) => FormatCell(asset, choice)
            , (choice) => FormatCell(asset, choice)
            );
            popup.SetEnabled(asset != null);
            popup.BindProperty(property.FindPropertyRelative("guid"));
            return popup;
        }
        private string FormatCell(ActionGraphGlobalSettings asset, string choice) {
            if (asset == null)
                return ActionType.UnknownActionName;
            if (string.IsNullOrEmpty(choice))
                return ActionType.NullActionName;
            var action = asset.actions.FirstOrDefault(a => a.guid == choice);
            if (action == null)
                return ActionType.UnknownActionName;
            return string.IsNullOrEmpty(action.name) ? action.type.Value.Name : action.name;
        }

    }
}
