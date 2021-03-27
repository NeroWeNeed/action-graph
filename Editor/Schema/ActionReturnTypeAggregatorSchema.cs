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
    public class ActionReturnTypeAggregatorSchema : IInitializable {
        [JsonProperty("data")]
        internal Dictionary<string, Dictionary<string, List<SerializableMethod>>> assemblyData = new Dictionary<string, Dictionary<string, List<SerializableMethod>>>();
        [JsonIgnore]
        public DictionaryView<string, Dictionary<string, List<SerializableMethod>>, Type, List<SerializableMethod>> data;

        public ActionReturnTypeAggregatorSchema() {
            data = new DictionaryView<string, Dictionary<string, List<SerializableMethod>>, Type, List<SerializableMethod>>(() => assemblyData, (old) => old.Values.Aggregate((a, b) => a.Concat(b).GroupBy(c => c.Key,c => c.Value).ToDictionary(d => d.Key,d => d.Aggregate((e,f) => e.Concat(f).ToList()))).ToDictionary(d => Type.GetType(d.Key), d => d.Value));
        }
        public void OnInit() {
            AssemblyAnalyzer.AnalyzeAssemblies(new ActionReturnTypeAggregatorFinder() { schema = this });
        }
    }
    public class ActionReturnTypeAggregatorProvider : IMethodProvider {

        public virtual List<SerializableMethod> GetMethods() {
            return ProjectUtility.GetOrCreateProjectAsset<ActionReturnTypeAggregatorSchema>().data.SelectMany(v => v.Value).ToList();
        }
    }
    public class ActionReturnTypeAggregatorProvider<TType> : ActionReturnTypeAggregatorProvider {
        public override List<SerializableMethod> GetMethods() {
            if (ProjectUtility.GetOrCreateProjectAsset<ActionReturnTypeAggregatorSchema>().data.TryGetValue(typeof(TType), out var result)) {
                return result;
            }
            else {
                return null;
            }
        }
    }
}