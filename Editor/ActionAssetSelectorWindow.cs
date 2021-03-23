using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor {
    public class ActionAssetSelectorWindow : EditorWindow {
        public const string Uxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionAssetSelector.uxml";
        public const string ItemUxml = "Packages/github.neroweneed.action-graph/Editor/Resources/ActionAssetSelectorItem.uxml";
        public static ActionAssetSelectorWindow ShowWindow(ActionAssetField field) {
            var window = ScriptableObject.CreateInstance<ActionAssetSelectorWindow>();
            window.titleContent = new GUIContent("Select");
            window.minSize = new Vector2(320, 480);
            window.ActionIdFilter = field.ActionId;
            window.Receiver = field;
            window.ShowUtility();
            //window.ShowPopup();
            //window.position = new Rect(new Vector2(field.worldBound.position.x,field.worldBound.position.y + EditorGUIUtility.singleLineHeight), new Vector2(field.worldBound.width, 480));

            return window;
        }
        private VisualTreeAsset itemVisualTreeAsset;
        private List<ActionAsset> assets = new List<ActionAsset>();
        private ActionId actionIdFilter;
        private string nameFilter = null;
        public ActionId ActionIdFilter
        {
            get => actionIdFilter; set
            {
                actionIdFilter = value;
                UpdateCache();
            }
        }
        public List<ActionAsset> Assets { get => assets; }
        private ListView listView;
        public ActionAssetField Receiver { get; set; }


        private void OnEnable() {
            EditorApplication.projectChanged += UpdateCache;
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree(rootVisualElement);
            itemVisualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ItemUxml);
            listView = rootVisualElement.Q<ListView>("items");
            listView.itemsSource = assets;
            listView.makeItem = CreateItem;
            listView.bindItem = BindItem;
            listView.unbindItem = UnbindItem;
            listView.onItemsChosen += UpdateReceiver;
            var content = rootVisualElement.Q<VisualElement>("content");
            content.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                listView.style.minHeight = evt.newRect.height;
            });
            rootVisualElement.Q<ToolbarSearchField>("search").RegisterValueChangedCallback(evt =>
            {
                nameFilter = evt.newValue;
                UpdateCache();
            });
            UpdateCache();
        }

        private void OnDisable() {
            EditorApplication.projectChanged -= UpdateCache;
        }
/*         void OnLostFocus() {
            this.Close();
        } */
        private VisualElement CreateItem() => itemVisualTreeAsset.CloneTree();
        private void BindItem(VisualElement element, int index) {
            var item = assets[index];
            if (item == null) {
                element.Q<Image>("icon").image = null;
                element.Q<Label>("text").text = ActionAssetField.NullAssetText;
            }
            else {
                var content = EditorGUIUtility.ObjectContent(assets[index], typeof(ActionAsset));
                element.Q<Image>("icon").image = ActionAssetEditor.AssetIcon;
                element.Q<Label>("text").text = content.text;
            }

        }
        private void UnbindItem(VisualElement element, int index) {
            element.Q<Image>("icon").image = null;
            element.Q<Label>("text").text = string.Empty;
        }
        private void UpdateReceiver(IEnumerable<object> selected) {
            if (Receiver != null) {
                Receiver.value = (ActionAsset)selected.First();
            }
            this.Close();
        }
        private void UpdateCache() {
            string filter = $"t:{nameof(ActionAsset)}";
            if (actionIdFilter.IsCreated) {
                filter += $" l:Action:{actionIdFilter}";
            }
            if (!string.IsNullOrEmpty(nameFilter)) {
                filter += $" name:{nameFilter}";
            }
            ActionAsset[] assets = AssetDatabase.FindAssets(filter).Select(guid => AssetDatabase.LoadAssetAtPath<ActionAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            this.assets.Clear();
            this.assets.Add(null);
            this.assets.AddRange(assets);
            listView.Refresh();
        }
    }
}