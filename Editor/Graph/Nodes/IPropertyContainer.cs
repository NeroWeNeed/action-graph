using System;
using System.Collections.Generic;

namespace NeroWeNeed.ActionGraph.Editor.Graph {
    public interface IPropertyContainer {
        public Dictionary<string, object> Properties { get; }
        
    }
    
}