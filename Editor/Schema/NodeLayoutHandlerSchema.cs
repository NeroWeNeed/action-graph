using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.Analyzer;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;

namespace NeroWeNeed.ActionGraph.Editor.Schema {
    [Serializable]
    [ProjectAsset(ProjectAssetType.Json)]
    public class NodeLayoutHandlerSchema : IInitializable {

        [JsonProperty("data")]
        internal Dictionary<string, Dictionary<string, SerializableType>> assemblyData = new Dictionary<string, Dictionary<string, SerializableType>>();
        [JsonIgnore]
        public DictionaryView<string, Dictionary<string, SerializableType>, Type, Lazy<NodeLayoutHandler>> data;

        public NodeLayoutHandlerSchema() {

            
            this.data = new DictionaryView<string, Dictionary<string, SerializableType>, Type, Lazy<NodeLayoutHandler>>(() => assemblyData, (old) => old.Values.Aggregate((a, b) => a.Concat(b).ToDictionary(c => c.Key, c => c.Value)).ToDictionary(d => Type.GetType(d.Key), d => new Lazy<NodeLayoutHandler>(() => (NodeLayoutHandler)Activator.CreateInstance(d.Value.Value))));

        }

        public void OnInit() {
            AssemblyAnalyzer.AnalyzeAssemblies(new NodeLayoutHandlerFinder() { schema = this });
        }
    }
}