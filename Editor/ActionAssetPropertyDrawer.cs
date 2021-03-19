using System.Linq;
using System.Reflection;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using static NeroWeNeed.ActionGraph.Editor.ActionGraphGlobalSettings;

namespace NeroWeNeed.ActionGraph.Editor {
    [CustomPropertyDrawer(typeof(ActionAsset))]
    public class ActionAssetPropertyDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            var field = new ActionAssetField();
            var attr = fieldInfo.GetCustomAttribute<ActionAssetAttribute>();
            var settings = ProjectUtility.GetProjectAsset<ActionGraphGlobalSettings>();
            if (attr != null && settings.TryGetActionInfo(attr.name, out ActionInfo info)) {
                field.ActionId = info.id;
            }
            field.Label = property.displayName;
            field.BindProperty(property);
            return field;
        }

    }
}