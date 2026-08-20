using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace AugmentaWebsocketClient
{
    /// <summary>
    /// Editor guard for the samples that ship a Visual Effect Graph.
    ///
    /// It does two things when one of those samples is imported:
    /// - offers to install the Visual Effect Graph package when it is missing, since the client package
    ///   itself does not depend on it;
    /// - force reimports the sample's .vfx assets once the sample scripts have compiled. Unity imports the
    ///   whole sample folder before compiling Assembly-CSharp, so a graph reading a [VFXType] struct is
    ///   compiled while that struct does not exist yet, and fails with an unknown type until the graph is
    ///   reimported.
    /// </summary>
    internal class AugmentaSampleImportGuard : AssetPostprocessor
    {
        /// <summary>
        /// Folder names of the samples shipping a .vfx. Both spellings are needed: the Package Manager
        /// copies a sample into a folder named after its package.json displayName, while a sample used from
        /// the repository keeps its Samples~ folder name. Add a new VFX sample here, under both names.
        /// </summary>
        private static readonly string[] vfxSampleFolders =
        {
            "SampleClusterFlow", "Cluster Flow",
            "SampleClusterField", "Cluster Field"
        };

        private const string vfxGraphPackage = "com.unity.visualeffectgraph";
        private const string vfxGraphAssembly = "Unity.VisualEffectGraph.Editor";

        // Session scoped: the pending graphs must survive the domain reload caused by the sample scripts
        // compiling, but a new Editor session has nothing left to fix.
        private const string pendingGraphsKey = "Augmenta.PendingGraphReimports";
        private const string packageAskedKey = "Augmenta.VfxGraphInstallAsked";

        private static AddRequest addRequest;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
                                                   string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Sample folders that received a script in this batch, keyed by the sample folder path
            var sampleFoldersWithScripts = new HashSet<string>();
            var importedGraphs = new List<string>();
            var touchedSample = false;

            foreach (var path in importedAssets)
            {
                if (!TryGetSampleFolder(path, out var sampleFolder))
                    continue;

                touchedSample = true;

                if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    sampleFoldersWithScripts.Add(sampleFolder);
                else if (path.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase))
                    importedGraphs.Add(path);
            }

            if (!touchedSample)
                return;

            CheckVisualEffectGraphPackage();

            // Only the graphs imported next to scripts can have been compiled against a missing type
            var pending = new List<string>();

            foreach (var path in importedGraphs)
            {
                if (TryGetSampleFolder(path, out var sampleFolder) && sampleFoldersWithScripts.Contains(sampleFolder))
                    pending.Add(path);
            }

            if (pending.Count == 0)
                return;

            var queued = SessionState.GetString(pendingGraphsKey, "");
            SessionState.SetString(pendingGraphsKey, queued.Length == 0 ? string.Join("\n", pending)
                                                                       : queued + "\n" + string.Join("\n", pending));
        }

        /// <summary>
        /// Runs on the domain reload that follows the sample scripts compiling, which is exactly when the
        /// queued graphs can be compiled against the types they need.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ReimportPendingGraphs()
        {
            var queued = SessionState.GetString(pendingGraphsKey, "");

            if (string.IsNullOrEmpty(queued))
                return;

            SessionState.EraseString(pendingGraphsKey);

            var paths = queued.Split('\n');

            EditorApplication.delayCall += () =>
            {
                AssetDatabase.StartAssetEditing();

                try
                {
                    foreach (var path in paths)
                    {
                        if (!string.IsNullOrEmpty(path))
                            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                Debug.Log("[Augmenta] Reimported the sample visual effects now that their scripts are " +
                          "compiled: " + string.Join(", ", paths));
            };
        }

        /// <summary>
        /// Returns true when the asset belongs to one of the samples shipping a graph, and gives back the
        /// path of that sample folder. Matches both the Package Manager layout
        /// (Assets/Samples/[package]/[version]/[sample]) and a sample used directly from the repository.
        /// </summary>
        private static bool TryGetSampleFolder(string path, out string sampleFolder)
        {
            sampleFolder = null;

            var segments = path.Split('/');
            var insideSamples = false;

            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i] == "Samples")
                {
                    insideSamples = true;
                    continue;
                }

                if (!insideSamples || Array.IndexOf(vfxSampleFolders, segments[i]) < 0)
                    continue;

                sampleFolder = string.Join("/", segments, 0, i + 1);
                return true;
            }

            return false;
        }

        private static void CheckVisualEffectGraphPackage()
        {
            if (IsVisualEffectGraphInstalled() || SessionState.GetBool(packageAskedKey, false))
                return;

            SessionState.SetBool(packageAskedKey, true);

            if (Application.isBatchMode)
            {
                LogMissingPackage();
                return;
            }

            // Let the import batch finish before opening a dialog
            EditorApplication.delayCall += () =>
            {
                var install = EditorUtility.DisplayDialog(
                    "Visual Effect Graph required",
                    "This Augmenta sample uses the Visual Effect Graph package, which is not installed in " +
                    "this project. Its scripts and effects will not compile without it.\n\n" +
                    "Install " + vfxGraphPackage + " now?",
                    "Install", "Later");

                if (install)
                    InstallVisualEffectGraph();
                else
                    LogMissingPackage();
            };
        }

        private static bool IsVisualEffectGraphInstalled()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == vfxGraphAssembly)
                    return true;
            }

            return false;
        }

        private static void InstallVisualEffectGraph()
        {
            if (addRequest != null)
                return;

            addRequest = Client.Add(vfxGraphPackage);
            EditorApplication.update += TrackInstallRequest;
        }

        private static void TrackInstallRequest()
        {
            if (addRequest == null || !addRequest.IsCompleted)
                return;

            EditorApplication.update -= TrackInstallRequest;

            if (addRequest.Status == StatusCode.Success)
                Debug.Log("[Augmenta] Installed " + addRequest.Result.packageId + " for the Augmenta samples.");
            else
                Debug.LogError("[Augmenta] Could not install " + vfxGraphPackage + ": " + addRequest.Error?.message);

            addRequest = null;
        }

        private static void LogMissingPackage()
        {
            Debug.LogWarning("[Augmenta] This sample needs the Visual Effect Graph package. Add " +
                             vfxGraphPackage + " from the Package Manager to use it.");
        }
    }
}
