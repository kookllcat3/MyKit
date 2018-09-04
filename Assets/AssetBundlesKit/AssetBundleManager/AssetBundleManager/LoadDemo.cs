using System.Collections;
using UnityEngine;

namespace AssetBundles.Manager
{
    public class LoadDemo : MonoBehaviour
    {
        [Header("測試載入Asset")]
        [Tooltip("沒值不會載入Asset")] [SerializeField] string assetBundle;
        [Tooltip("沒值不會載入Asset")] [SerializeField] string assetName;

        [Header("測試載入Scene")]
        [Tooltip("沒值不會載入Scene")] [SerializeField] string sceneAssetBundle;
        [Tooltip("沒值不會載入Scene")] [SerializeField] string sceneName;
        [SerializeField] bool isAdditive;

        [Header("測試Variant")]
        [Tooltip("沒值不會有Variant")] [SerializeField] string[] variants;

        // The following are used only if app slicing is not enabled.
        private string[] activeVariants;
        private bool bundlesLoaded;             // used to remove the loading buttons

        void Awake()
        {
            activeVariants = new string[1];
            bundlesLoaded = false;
        }

        private void Start()
        {
            if (variants.Length == 0)
            {
                StartCoroutine(BeginExample());
            }
        }

        void OnGUI()
        {
            if (!bundlesLoaded)
            {
                GUILayout.Space(20);
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.BeginVertical();

                for (int i = 0; i < variants.Length; i++)
                {
                    if (GUILayout.Button("Load " + variants[i]))
                    {
                        activeVariants[0] = variants[i];
                        bundlesLoaded = true;
                        StartCoroutine(BeginExample());
                        Debug.Log("Loading " + variants[i]);
                    }

                    if (variants.Length > 1 && i < variants.Length - 1)
                    {
                        GUILayout.Space(5);
                    }
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        // Use this for initialization
        IEnumerator BeginExample()
        {
            yield return StartCoroutine(Initialize());

            // Set active variants.
            AssetBundleManager.ActiveVariants = activeVariants;

            // Load variant asset which depends on variants.
            if (!string.IsNullOrEmpty(assetBundle) && !string.IsNullOrEmpty(assetName))
            {
                yield return StartCoroutine(InstantiateGameObjectAsync(assetBundle, assetName));
            }

            // Load variant level which depends on variants.
            if (!string.IsNullOrEmpty(sceneAssetBundle) && !string.IsNullOrEmpty(sceneName))
            {
                yield return StartCoroutine(InitializeLevelAsync(sceneName, isAdditive));
            }
        }

        // Initialize the downloading url and AssetBundleManifest object.
        IEnumerator Initialize()
        {
            // Initialize AssetBundleManifest which loads the AssetBundleManifest object.
            var request = AssetBundleManager.Initialize();

            if (request != null)
                yield return StartCoroutine(request);
        }

        IEnumerator InstantiateGameObjectAsync(string assetBundleName, string assetName)
        {
            // This is simply to get the elapsed time for this phase of AssetLoading.
            float startTime = Time.realtimeSinceStartup;

            // Load asset from assetBundle.
            AssetBundleLoadAssetOperation request = AssetBundleManager.LoadAssetAsync(assetBundleName, assetName, typeof(GameObject));
            if (request == null)
                yield break;
            yield return StartCoroutine(request);

            // Get the asset.
            GameObject prefab = request.GetAsset<GameObject>();

            if (prefab != null)
                GameObject.Instantiate(prefab);

            // Calculate and display the elapsed time.
            float elapsedTime = Time.realtimeSinceStartup - startTime;
            Debug.Log(assetName + (prefab == null ? " was not" : " was") + " loaded successfully in " + elapsedTime + " seconds");
        }

        IEnumerator InitializeLevelAsync(string levelName, bool isAdditive)
        {
            // This is simply to get the elapsed time for this phase of AssetLoading.
            float startTime = Time.realtimeSinceStartup;

            // Load level from assetBundle.
            AssetBundleLoadOperation request = AssetBundleManager.LoadLevelAsync(sceneAssetBundle, levelName, isAdditive);
            if (request == null)
                yield break;

            yield return StartCoroutine(request);

            // Calculate and display the elapsed time.
            float elapsedTime = Time.realtimeSinceStartup - startTime;
            Debug.Log("Finished loading scene " + levelName + " in " + elapsedTime + " seconds");
        }
    }
}
