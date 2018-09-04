using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.AssetGraph;
#endif
using System;
using System.Collections.Generic;

/*
 	In this demo, we demonstrate:
	1.	Automatic asset bundle dependency resolving & loading.
		It shows how to use the manifest assetbundle like how to get the dependencies etc.
	2.	Automatic unloading of asset bundles (When an asset bundle or a dependency thereof is no longer needed, the asset bundle is unloaded)
	3.	Editor simulation. A bool defines if we load asset bundles from the project or are actually using asset bundles(doesn't work with assetbundle variants for now.)
		With this, you can player in editor mode without actually building the assetBundles.
	4.	Optional setup where to download all asset bundles
	5.	Build pipeline build postprocessor, integration so that building a player builds the asset bundles and puts them into the player data (Default implmenetation for loading assetbundles from disk on any platform)
	6.	Use WWW.LoadFromCacheOrDownload and feed 128 bit hash to it when downloading via web
		You can get the hash from the manifest assetbundle.
	7.	AssetBundle variants. A prioritized list of variants that should be used if the asset bundle with that variant exists, first variant in the list is the most preferred etc.
*/

namespace AssetBundles.Manager
{
    // Loaded assetBundle contains the references count which can be used to unload dependent assetBundles automatically.
    public class LoadedAssetBundle
    {
        public AssetBundle m_AssetBundle;
        public int m_ReferencedCount;

        public LoadedAssetBundle(AssetBundle assetBundle, int refCount)
        {
            m_AssetBundle = assetBundle;
            m_ReferencedCount = refCount;
        }
    }

    // Class takes care of loading assetBundle and its dependencies automatically, loading variants automatically.
    public class AssetBundleManager : MonoBehaviour
    {
        public enum LogMode { All, JustErrors };
        public enum LogType { Info, Warning, Error };

        static LogMode m_LogMode = LogMode.All;
        static string[] m_ActiveVariants = { };
        static AssetBundleManifest m_AssetBundleManifest = null;

        static Dictionary<string, LoadedAssetBundle> m_LoadedAssetBundles = new Dictionary<string, LoadedAssetBundle>();
        static Dictionary<string, int> abRefCounts = new Dictionary<string, int>();
        static Dictionary<string, UnityWebRequest> m_DownloadingWWWs = new Dictionary<string, UnityWebRequest>();
        static List<string> keysToRemove = new List<string>();
        static Dictionary<string, string> m_DownloadingErrors = new Dictionary<string, string>();
        static List<AssetBundleLoadOperation> m_InProgressOperations = new List<AssetBundleLoadOperation>();
        static Dictionary<string, string[]> m_Dependencies = new Dictionary<string, string[]>();

        public static LogMode logMode
        {
            get { return m_LogMode; }
            set { m_LogMode = value; }
        }

        // Variants which is used to define the active variants.
        public static string[] ActiveVariants
        {
            get { return m_ActiveVariants; }
            set { m_ActiveVariants = value; }
        }

        // AssetBundleManifest object which can be used to load the dependecies and check suitable assetBundle variants.
        public static AssetBundleManifest AssetBundleManifestObject
        {
            set { m_AssetBundleManifest = value; }
        }

        private static void Log(LogType logType, string text)
        {
            //if (logType == LogType.Error)
            //    Debug.LogError("[AssetBundleManager] " + text);
            //else if (m_LogMode == LogMode.All)
            //    Debug.Log("[AssetBundleManager] " + text);
        }

#if UNITY_EDITOR
        // Flag to indicate if we want to simulate assetBundles in Editor without building them actually.
        public static bool SimulateAssetBundleInEditor
        {
            get
            {
                return Settings.Mode != Settings.AssetBundleManagerMode.Server;
            }
        }

        public static bool CleanCacheOnPlay
        {
            get
            {
                return Settings.ClearCacheOnPlay;
            }
        }

#endif

        private static string GetStreamingAssetsPath()
        {
            if (Application.isEditor)
                return "file://" + System.Environment.CurrentDirectory.Replace("\\", "/"); // Use the build output folder directly.
#if !UNITY_5_4_OR_NEWER
            else if (Application.isWebPlayer)
				return System.IO.Path.GetDirectoryName(Application.absoluteURL).Replace("\\", "/")+ "/StreamingAssets";
#endif
            else if (Application.isMobilePlatform || Application.isConsolePlatform)
                return Application.streamingAssetsPath;
            else // For standalone player.
                return "file://" + Application.streamingAssetsPath;
        }

        // Get loaded AssetBundle, only return vaild object when all the dependencies are downloaded successfully.
        static public LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName, out string error)
        {
            if (m_DownloadingErrors.TryGetValue(assetBundleName, out error))
                return null;

            LoadedAssetBundle bundle = null;
            m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
            if (bundle == null)
                return null;

            // No dependencies are recorded, only the bundle itself is required.
            string[] dependencies = null;
            if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
                return bundle;

            // Make sure all dependencies are loaded
            foreach (var dependency in dependencies)
            {
                if (m_DownloadingErrors.TryGetValue(assetBundleName, out error))
                    return bundle;

                // Wait all the dependent assetBundles being loaded.
                LoadedAssetBundle dependentBundle;
                m_LoadedAssetBundles.TryGetValue(dependency, out dependentBundle);
                if (dependentBundle == null)
                    return null;
            }

            return bundle;
        }

        // Load AssetBundleManifest.
        static public AssetBundleLoadManifestOperation Initialize()
        {
#if UNITY_EDITOR
            Log(LogType.Info, "Simulation Mode: " + (SimulateAssetBundleInEditor ? "Enabled" : "Disabled"));
#endif

            var go = new GameObject("AssetBundleManager", typeof(AssetBundleManager));
            DontDestroyOnLoad(go);

#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't need the manifest assetBundle.
            if (SimulateAssetBundleInEditor)
                return null;
#endif

#if UNITY_EDITOR
            if (CleanCacheOnPlay)
            {
                Log(LogType.Info, "AssetBundleManager cleaned local cache.");
#if UNITY_2017_1_OR_NEWER
                Caching.ClearCache();
#else
                Caching.CleanCache();
#endif
            }
#endif

            LoadAssetBundle(Settings.CurrentSetting.ManifestFileName, true);
            var operation = new AssetBundleLoadManifestOperation(Settings.CurrentSetting.ManifestFileName, "AssetBundleManifest", typeof(AssetBundleManifest));

            m_InProgressOperations.Add(operation);

            return operation;
        }

        // Load AssetBundle and its dependencies.
        static protected void LoadAssetBundle(string assetBundleName, bool isLoadingAssetBundleManifest = false)
        {
            Log(LogType.Info, "Loading Asset Bundle " + (isLoadingAssetBundleManifest ? "Manifest: " : ": ") + assetBundleName);

#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to really load the assetBundle and its dependencies.
            if (SimulateAssetBundleInEditor)
                return;
#endif

            if (!isLoadingAssetBundleManifest)
            {
                if (m_AssetBundleManifest == null)
                {
                    Debug.LogError("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                    return;
                }
            }

            // Check if the assetBundle has already been processed.
            bool isAlreadyProcessed = LoadAssetBundleInternal(assetBundleName, isLoadingAssetBundleManifest);

            // Load dependencies.
            if (!isAlreadyProcessed && !isLoadingAssetBundleManifest)
                LoadDependencies(assetBundleName);
        }

        // Remaps the asset bundle name to the best fitting asset bundle variant.
        static protected string RemapVariantName(string assetBundleName)
        {
            string[] bundlesWithVariant = null;
#if UNITY_EDITOR
            if (SimulateAssetBundleInEditor)
            {
                bundlesWithVariant = AssetDatabase.GetAllAssetBundleNames();
            }
            else
#endif
            {
                if (m_AssetBundleManifest == null)
                {
                    Debug.LogError("AssetBundle Manifest is not loaded. Aborting RemapVariantName()");
                    return null;
                }
                bundlesWithVariant = m_AssetBundleManifest.GetAllAssetBundlesWithVariant();
            }

            string[] split = assetBundleName.Split('.');

            int bestFit = int.MaxValue;
            int bestFitIndex = -1;
            // Loop all the assetBundles with variant to find the best fit variant assetBundle.
            for (int i = 0; i < bundlesWithVariant.Length; i++)
            {
                string[] curSplit = bundlesWithVariant[i].Split('.');

                if (curSplit.Length < 2)
                {
                    continue;
                }

                if (curSplit[0] != split[0])
                    continue;

                int found = System.Array.IndexOf(m_ActiveVariants, curSplit[1]);

                // If there is no active variant found. We still want to use the first 
                if (found == -1)
                    found = int.MaxValue - 1;

                if (found < bestFit)
                {
                    bestFit = found;
                    bestFitIndex = i;
                }
            }

            if (bestFit == int.MaxValue - 1)
            {
                Debug.LogWarning("Ambigious asset bundle variant chosen because there was no matching active variant: " + bundlesWithVariant[bestFitIndex]);
            }

            if (bestFitIndex != -1)
            {
                return bundlesWithVariant[bestFitIndex];
            }
            else
            {
                return assetBundleName;
            }
        }

        // Where we actuall call WWW to download the assetBundle.
        static protected bool LoadAssetBundleInternal(string assetBundleName, bool isLoadingAssetBundleManifest)
        {
            // Already loaded.
            LoadedAssetBundle bundle = null;
            m_LoadedAssetBundles.TryGetValue(assetBundleName, out bundle);
            if (bundle != null)
            {
                bundle.m_ReferencedCount++;
                return true;
            }

            // @TODO: Do we need to consider the referenced count of WWWs?
            // In the demo, we never have duplicate WWWs as we wait LoadAssetAsync()/LoadLevelAsync() to be finished before calling another LoadAssetAsync()/LoadLevelAsync().
            // But in the real case, users can call LoadAssetAsync()/LoadLevelAsync() several times then wait them to be finished which might have duplicate WWWs.
            if (m_DownloadingWWWs.ContainsKey(assetBundleName))
            {
                abRefCounts[assetBundleName]++;
                return true;
            }
                

            //資料下載失敗時，重新下載要清除掉錯誤紀錄
            if (m_DownloadingErrors.ContainsKey(assetBundleName))
            {
                m_DownloadingErrors.Remove(assetBundleName);
            }

            UnityWebRequest download = null;
            string url = Settings.CurrentSetting.ServerURL + assetBundleName;

            // For manifest assetbundle, always download it as we don't have hash for it.
            if (isLoadingAssetBundleManifest)
                download = UnityWebRequest.GetAssetBundle(url);
            else
                download = UnityWebRequest.GetAssetBundle(url, m_AssetBundleManifest.GetAssetBundleHash(assetBundleName), 0);

            download.SendWebRequest();
            m_DownloadingWWWs.Add(assetBundleName, download);
            abRefCounts.Add(assetBundleName, 1);

            return false;
        }

        // Where we get all the dependencies and load them all.
        static protected void LoadDependencies(string assetBundleName)
        {
            if (m_AssetBundleManifest == null)
            {
                Debug.LogError("Please initialize AssetBundleManifest by calling AssetBundleManager.Initialize()");
                return;
            }

            // Get dependecies from the AssetBundleManifest object..
            string[] dependencies = m_AssetBundleManifest.GetAllDependencies(assetBundleName);
            if (dependencies.Length == 0)
                return;

            for (int i = 0; i < dependencies.Length; i++)
                dependencies[i] = RemapVariantName(dependencies[i]);

            // Record and load all dependencies.
            if (!m_Dependencies.ContainsKey(assetBundleName))
            {
                m_Dependencies.Add(assetBundleName, dependencies);
            }          
            //Debug.Log("[Dependency]" + assetBundleName + " => " + string.Concat(dependencies));
            for (int i = 0; i < dependencies.Length; i++)
            {
                bool loading = LoadAssetBundleInternal(dependencies[i], false);
                //Debug.Log("[Loading Dependency] loading " + dependencies[i] + ", load over= " + loading);
            }
        }

        // Unload assetbundle and its dependencies.
        static public void UnloadAssetBundle(string assetBundleName)
        {
#if UNITY_EDITOR
            // If we're in Editor simulation mode, we don't have to load the manifest assetBundle.
            if (SimulateAssetBundleInEditor)
                return;
#endif

            //Debug.Log(m_LoadedAssetBundles.Count + " assetbundle(s) in memory before unloading " + assetBundleName);

            UnloadAssetBundleInternal(assetBundleName);
            UnloadDependencies(assetBundleName);

            //Debug.Log(m_LoadedAssetBundles.Count + " assetbundle(s) in memory after unloading " + assetBundleName);
        }

        static protected void UnloadDependencies(string assetBundleName)
        {
            string[] dependencies = null;
            if (!m_Dependencies.TryGetValue(assetBundleName, out dependencies))
                return;

            // Loop dependencies.
            foreach (var dependency in dependencies)
            {
                UnloadAssetBundleInternal(dependency);
            }

            m_Dependencies.Remove(assetBundleName);
        }

        static protected void UnloadAssetBundleInternal(string assetBundleName)
        {
            string error;
            LoadedAssetBundle bundle = GetLoadedAssetBundle(assetBundleName, out error);
            if (bundle == null)
                return;

            if (--bundle.m_ReferencedCount == 0)
            {
                bundle.m_AssetBundle.Unload(true);
                m_LoadedAssetBundles.Remove(assetBundleName);

                Log(LogType.Info, assetBundleName + " has been unloaded successfully");
            }
        }
        
        void Update()
        {
            // Collect all the finished WWWs.
            keysToRemove.Clear();
            foreach (var keyValue in m_DownloadingWWWs)
            {
                UnityWebRequest download = keyValue.Value;

                // If downloading fails.
                if (download.isNetworkError || download.isHttpError)
                {
                    m_DownloadingErrors.Add(keyValue.Key, string.Format("Failed downloading bundle {0} from {1}: {2}", keyValue.Key, download.url, download.error));
                    keysToRemove.Add(keyValue.Key);
                    continue;
                }

                // If downloading succeeds.
                if (download.isDone)
                {
                    keysToRemove.Add(keyValue.Key);
                    AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(download);                     
                    if (bundle == null)
                    {
                        m_DownloadingErrors.Add(keyValue.Key, string.Format("{0} is not a valid asset bundle.", keyValue.Key));                       
                        continue;
                    }

                    //Debug.Log("Downloading " + keyValue.Key + " is done at frame " + Time.frameCount);
                    m_LoadedAssetBundles.Add(keyValue.Key, new LoadedAssetBundle(bundle, abRefCounts[keyValue.Key]));
                }
            }

            // Remove the finished WWWs.
            foreach (var key in keysToRemove)
            {
                UnityWebRequest download = m_DownloadingWWWs[key];
                m_DownloadingWWWs.Remove(key);
                abRefCounts.Remove(key);
                download.Dispose();
            }

            // Update all in progress operations
            for (int i = 0; i < m_InProgressOperations.Count;)
            {
                if (!m_InProgressOperations[i].Update())
                {
                    m_InProgressOperations.RemoveAt(i);
                }
                else
                    i++;
            }
        }

        // Load asset from the given assetBundle.
        static public AssetBundleLoadAssetOperation LoadAssetAsync(string assetBundleName, string assetName, System.Type type)
        {
            Log(LogType.Info, "Loading " + assetName + " from " + assetBundleName + " bundle");

            AssetBundleLoadAssetOperation operation = null;
            assetBundleName = RemapVariantName(assetBundleName);
            if (assetBundleName == null)
            {
                return null;
            }

#if UNITY_EDITOR
            if (SimulateAssetBundleInEditor)
            {
                string[] assetPaths = null;
                if (Settings.Mode == Settings.AssetBundleManagerMode.SimulationModeGraphTool)
                {
                    assetPaths = AssetBundleBuildMap.GetBuildMap().GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, assetName);
                }
                else
                {
                    assetPaths = AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(assetBundleName, assetName);
                }

                if (assetPaths.Length == 0)
                {
                    Debug.LogError("There is no asset with name \"" + assetName + "\" in " + assetBundleName);
                    return null;
                }

                // @TODO: Now we only get the main object from the first asset. Should consider type also.
                Object target = AssetDatabase.LoadMainAssetAtPath(assetPaths[0]);
                operation = new AssetBundleLoadAssetOperationSimulation(target);
            }
            else
#endif
            {
                LoadAssetBundle(assetBundleName);
                operation = new AssetBundleLoadAssetOperationFull(assetBundleName, assetName, type);

                m_InProgressOperations.Add(operation);
            }

            return operation;
        }

        // Load level from the given assetBundle.
        static public AssetBundleLoadOperation LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive)
        {
            Log(LogType.Info, "Loading " + levelName + " from " + assetBundleName + " bundle");

            AssetBundleLoadOperation operation = null;

            assetBundleName = RemapVariantName(assetBundleName);
            if (assetBundleName == null)
            {
                return null;
            }

#if UNITY_EDITOR
            if (SimulateAssetBundleInEditor)
            {
                operation = new AssetBundleLoadLevelOperationSimulation(assetBundleName, levelName, isAdditive);
            }
            else
#endif
            {
                LoadAssetBundle(assetBundleName);
                operation = new AssetBundleLoadLevelOperationFull(assetBundleName, levelName, isAdditive);

                m_InProgressOperations.Add(operation);
            }

            return operation;
        }

        //預載AssetBundles, onDone return true = error
        static public void PreLoadAssetAsync(Action<float> onProgress, Action<bool> onDone, params string[] abNames)
        {
#if UNITY_EDITOR
            if (SimulateAssetBundleInEditor)
            {
                if (onProgress != null)
                    onProgress(1f);

                if (onDone != null)
                    onDone(false);
            }
            else
#endif
            {            
                List<string> beforeKeyList = new List<string>(m_DownloadingWWWs.Keys);

                foreach (var item in abNames)
                {
                    string abName = RemapVariantName(item);
                    if (abName == null)
                    {
                        if (onDone != null)    
                            onDone(true);              
                        return;
                    }
                    else
                    {
                        LoadAssetBundle(abName);
                    }
                }

                List<string> keyList = new List<string>();
                foreach (var item in m_DownloadingWWWs)
                {
                    if (!beforeKeyList.Contains(item.Key))
                        keyList.Add(item.Key);
                }

                var operation = new AssetBundlePreloadOperation(abNames, keyList, onProgress, onDone);
                m_InProgressOperations.Add(operation);            
            }
        }

        static public bool TryGetDwonload(string key, out UnityWebRequest www)
        {
            return m_DownloadingWWWs.TryGetValue(key, out www);
        }

    } // End of AssetBundleManager.
}