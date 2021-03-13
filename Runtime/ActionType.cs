using System;

namespace NeroWeNeed.ActionGraph {
    [Serializable]
    public struct ActionType {
        public const string NullActionName = "None";
        public const string UnknownActionName = "Unknown Action";
        public string guid;
    }
}