using System;
using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.ActionGraph.Editor.Analyzer;
using NeroWeNeed.Commons;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using Newtonsoft.Json;

namespace NeroWeNeed.ActionGraph.Editor.Schema {
    [Serializable]
    [ProjectAsset(ProjectAssetType.Json)]
    public class ActionArgumentComponentSchema : IInitializable {
        [JsonProperty("data")]
        internal Dictionary<string, Dictionary<string, Dictionary<string, ActionArgumentComponent>>> assemblyData = new Dictionary<string, Dictionary<string, Dictionary<string, ActionArgumentComponent>>>();
        [JsonIgnore]
        public DictionaryView<string, Dictionary<string, Dictionary<string, ActionArgumentComponent>>, Type, Dictionary<string, ActionArgumentComponent>> data;
        public ActionArgumentComponentSchema() {
            this.data = new DictionaryView<string, Dictionary<string, Dictionary<string, ActionArgumentComponent>>, Type, Dictionary<string, ActionArgumentComponent>>(() => assemblyData, (old) => old.Values.SelectMany(a => a.AsEnumerable()).GroupBy(a => a.Key, a => a.Value).Select(a => (Type.GetType(a.Key), a.Aggregate((b, c) => b.Concat(c).ToDictionary(d => d.Key, d => d.Value)))).ToDictionary(a => a.Item1, a => a.Item2));
        }
        public void OnInit() {
            AssemblyAnalyzer.AnalyzeAssemblies(new ActionArgumentComponentFinder() { schema = this });
        }
        [Serializable]
        public struct ActionArgumentComponent {
            public SerializableType delegateType;
            public SerializableType componentType;
            public string parameterName;
            public string fieldName;
            public bool singletonTarget;
        }


    }
}