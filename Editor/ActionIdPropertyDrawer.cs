using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    [CustomPropertyDrawer(typeof(ActionId))]
    public class ActionIdPropertyDrawer : PropertyDrawer {
    
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var settings = ProjectUtility.GetProjectSettings<ActionGraphGlobalSettings>();
            var definitions = ActionDefinitionAsset.LoadAll().ToArray();
            List<string> guids = definitions.Select(a => a.id.guid).ToList();
            guids.Insert(0, string.Empty);
            var popup = new PopupField<string>(property.displayName, guids, property.FindPropertyRelative("guid").stringValue, (choice) => FormatCell(definitions, choice)
            , (choice) => FormatCell(definitions, choice)
            );
            popup.SetEnabled(settings != null);
            popup.BindProperty(property.FindPropertyRelative("guid"));
            return popup;
        }
        private string FormatCell(ActionDefinitionAsset[] definitions, string choice) {
            if (definitions?.IsEmpty() != false)
                return ActionId.UnknownActionName;
            if (string.IsNullOrEmpty(choice))
                return ActionId.NullActionName;
            var action = System.Array.Find(definitions, a => a.id.guid == choice);
            if (action == null)
                return ActionId.UnknownActionName;
            return string.IsNullOrEmpty(action.displayName) ? action.delegateType.Value.Name : action.displayName;
        }

    }
}
