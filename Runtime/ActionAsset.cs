using System;
using System.Collections.Generic;
using System.Linq;
using NeroWeNeed.Commons;
using UnityEngine;

namespace NeroWeNeed.ActionGraph {
    [CreateAssetMenu(fileName = "ActionAsset", menuName = "ActionAsset", order = 0)]
    public class ActionAsset : ScriptableObject {

        public ActionType action;
        public TextAsset json;
        
    }
}