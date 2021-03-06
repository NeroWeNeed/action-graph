using System;
using System.Collections.Generic;
using System.Reflection;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public class ActionGraphModel {

        public ContextInfo context = new ContextInfo();
        public Dictionary<string, NodeData> nodes = new Dictionary<string, NodeData>();
        public Dictionary<string, ActionInfo> actionInfo = new Dictionary<string, ActionInfo>();
        public TData GetData<TData>(string guid) where TData : NodeData => (TData)nodes[guid];
        public NodeData this[string guid]
        {
            get => nodes[guid];
            set => nodes[guid] = value;
        }
        public void Clear() {
            this.nodes.Clear();
            this.actionInfo.Clear();
            this.context.Clear();
        }






        public struct VariableInfo {
            public string action;
            public string path;
            public SerializableType type;
        }
        public struct ActionInfo {
            public SerializableType type;
            public Dictionary<string, VariableInfo> variables;
        }

    }
    [Serializable]
    public struct ActionModule {
        public ActionAsset asset;
        public ActionDefinitionAsset definitionAsset;
        public ActionId action;
        public string guid;
        public string name;

        public ActionModule(ActionAsset asset, ActionDefinitionAsset definitionAsset, string name) {
            this.asset = asset;
            this.action = asset.actionId;
            this.guid = Guid.NewGuid().ToString("N");
            this.name = name;
            this.definitionAsset = definitionAsset;
        }
    }
    [Serializable]
    public class ContextInfo {
        public string containerGuid;
        [JsonIgnore]
        private ScriptableObject container;
        [JsonIgnore]
        public ScriptableObject Container
        {
            get
            {
                if (container == null && !string.IsNullOrWhiteSpace(containerGuid)) {
                    container = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(containerGuid));
                }
                return container;
            }
            set
            {
                container = value;
                containerGuid = value == null ? null : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
            }
        }
        public List<ActionModule> modules = new List<ActionModule>();
        public void Clear() {
            containerGuid = null;
            modules.Clear();
        }
    }

    public class ActionModuleSerializer : JsonConverter<ActionModule> {

        public override ActionModule ReadJson(JsonReader reader, Type objectType, ActionModule existingValue, bool hasExistingValue, JsonSerializer serializer) {
            if (reader.TokenType == JsonToken.StartObject) {
                var obj = JObject.Load(reader);
                var target = hasExistingValue ? existingValue : new ActionModule();
                target.asset = AssetDatabase.LoadAssetAtPath<ActionAsset>(AssetDatabase.GUIDToAssetPath(obj[nameof(ActionModule.asset)].Value<string>()));
                target.definitionAsset = target.asset != null ? ActionDefinitionAsset.Load(target.asset.actionId) : null;
                target.guid = obj[nameof(ActionModule.guid)].Value<string>();
                target.action = target.asset?.actionId ?? default;
                target.name = obj[nameof(ActionModule.name)].Value<string>();
                return target;
            }
            else {
                throw new JsonException();
            }
        }

        public override void WriteJson(JsonWriter writer, ActionModule value, JsonSerializer serializer) {
            var assetPath = AssetDatabase.GetAssetPath(value.asset);
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(ActionModule.asset));
            writer.WriteValue(string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.GUIDFromAssetPath(assetPath).ToString());
            writer.WritePropertyName(nameof(ActionModule.guid));
            writer.WriteValue(value.guid);
            writer.WritePropertyName(nameof(ActionModule.name));
            writer.WriteValue(value.name);
            writer.WriteEndObject();
        }
    }
}