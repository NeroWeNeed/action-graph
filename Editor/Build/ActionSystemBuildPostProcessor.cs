using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;

namespace NeroWeNeed.ActionGraph.Editor {
    public static class ActionSystemBuildPostProcessor {

        [PostProcessBuild(-1)]
        public static void OnPostprocessBuild(BuildTarget target, string path) {
            
            Debug.Log(path);
        }
    }
}