using NeroWeNeed.Commons.Editor;
using UnityEditor;

namespace NeroWeNeed.ActionGraph.Editor {
    public static class ActionAssetUtility {
        public static string GetArtifactPath(this ActionAsset actionAsset) => GetArtifactFile(actionAsset, ProjectUtility.GetOrCreateProjectSettings<ActionGraphGlobalSettings>());
        public static string GetArtifactFile(this ActionAsset actionAsset, ActionGraphGlobalSettings settings) {
            var path = AssetDatabase.GetAssetPath(actionAsset);
            if (string.IsNullOrEmpty(path)) {
                return null;
            }
            var importer = AssetImporter.GetAtPath(path);
            var guid = importer.userData;
            if (string.IsNullOrEmpty(guid)) {
                return settings.CreateArtifactPath();
            }
            var artifactPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(artifactPath)) {
                return settings.CreateArtifactPath();
            }
            return artifactPath;
        }

    }
}