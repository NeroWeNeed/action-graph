using System.Collections.Generic;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public interface INodeOutput {
        public string Next { get; set; }

    }
    public interface IActionElement {
        public ActionId ActionId { get; }
    }
}