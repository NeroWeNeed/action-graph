using System;
using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.Commons.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    public class ActionAssetField : BindableElement, INotifyValueChanged<UnityEngine.Object> {
        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionAssetField.uxml";
        public const string Uss = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionAssetField.uss";
        public const string ClassName = "action-asset-field";
        public const string InputClassName = "action-asset-field--input";
        public const string AssetTextClassName = "action-asset-field--name";
        public const string AssetIconClassName = "action-asset-field--icon";
        public const string LabelClassName = "action-asset-field--label";
        public const string SelectorClassName = "action-asset-field--selector";
        public const string DisplayClassName = "action-asset-field--display";
        internal const string NullAssetText = "None (Action Asset)";
        private ActionAsset rawValue = null;
        private ActionId actionId;
        private Label assetTextElement;
        private Label labelElement;
        private Image selectorElement;
        private VisualElement displayElement;
        private Image assetIconElement;
        
        public ActionId ActionId { get => actionId; set => actionId = value; }
        public string Label
        {
            get => labelElement.text; set
            {
                if (string.IsNullOrEmpty(value)) {
                    labelElement.SetEnabled(false);
                    labelElement.text = string.Empty;
                    labelElement.visible = false;
                }
                else {
                    labelElement.SetEnabled(true);
                    labelElement.text = value;
                    labelElement.visible = true;
                }
            }
        }
        public ActionAssetField(ActionId actionId, string label) {
        }
        public ActionAssetField() {
            Init();
        }
        public UnityEngine.Object value
        {
            get => rawValue; set
            {
                if (EqualityComparer<UnityEngine.Object>.Default.Equals(rawValue, value)) {
                    return;
                }
                if (base.panel != null) {
                    using ChangeEvent<UnityEngine.Object> changeEvent = ChangeEvent<UnityEngine.Object>.GetPooled(rawValue, value);
                    changeEvent.target = this;
                    SetValueWithoutNotify(value);
                    SendEvent(changeEvent);
                }
                else {
                    SetValueWithoutNotify(value);
                }
            }
        }

        internal void Init(ActionId actionId = default, string label = null) {
            this.AddToClassList(ClassName);
            var selectorIcon = EditorGUIUtility.IconContent("stylesheets/northstar/images/d_pick_uielements.png");

            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree(this);
            this.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(Uss));
            assetTextElement = this.Q<Label>(null, AssetTextClassName);
            assetIconElement = this.Q<Image>(null, AssetIconClassName);
            selectorElement = this.Q<Image>(null, SelectorClassName);
            labelElement = this.Q<Label>(null, LabelClassName);
            selectorElement.image = selectorIcon.image;
            displayElement = this.Q<VisualElement>(null, DisplayClassName);
            assetTextElement.text = NullAssetText;
            assetTextElement.pickingMode = PickingMode.Ignore;
            assetIconElement.pickingMode = PickingMode.Ignore;
            displayElement.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (value != null) {
                    if (evt.clickCount == 2) {
                        EditorGUIUtility.PingObject(value);
                    }
                }
            });
            selectorElement.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.pressedButtons == 1) {
                    ActionAssetSelectorWindow.ShowWindow(this);
                }
            });
            this.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                var obj = DNDValidation(DragAndDrop.objectReferences);
                if (obj != null) {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                }
            });
            this.RegisterCallback<DragPerformEvent>(evt =>
            {
                var obj = DNDValidation(DragAndDrop.objectReferences);
                if (obj != null) {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                    this.value = obj;
                    DragAndDrop.AcceptDrag();
                }
            });
            Label = label;
            ActionId = actionId;
        }
        private UnityEngine.Object DNDValidation(UnityEngine.Object[] objs) {
            if (objs == null)
                return null;
            UnityEngine.Object obj;
            if (actionId.IsCreated) {
                obj = objs.OfType<ActionAsset>().FirstOrDefault(o => o.actionId == actionId);
            }
            else {
                obj = objs.OfType<ActionAsset>().FirstOrDefault();
            }
            return obj;
        }

        public void SetValueWithoutNotify(UnityEngine.Object newValue) {
            this.rawValue = (ActionAsset)newValue;
            var content = EditorGUIUtility.ObjectContent(newValue, typeof(ActionAsset));

            assetTextElement.text = content.text;
            assetIconElement.image = ActionAssetEditor.AssetIcon;
        }

    }
}