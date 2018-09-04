using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;


public class DownloadAndSave
{
    static public string AssetDirectoryPath
    {
        get
        {
            return Application.persistentDataPath + Path.DirectorySeparatorChar;
        }
    }
    //onDone return true = error
    public IEnumerator Download(string url, string fileName, Action<float> onProgress, Action<bool> onDone)
    {
        bool error = false;

        if (!CheckHasFile(fileName))
        {
            using (UnityWebRequest www = UnityWebRequest.Get(url + fileName))
            {
                www.SendWebRequest();
                //Debug.Log(www.url);
                while (!www.isDone)
                {
                    if (onProgress != null)
                    {
                        onProgress(www.downloadProgress);
                    }

                    yield return null;
                }

                if (www.isNetworkError || www.isHttpError)
                {
                    error = true;
                }
                else
                {
                    CacheFile(fileName, www.downloadHandler.data);
                }
            }
        }

        if (onDone != null)
        {
            onDone(error);
        }
    }

    bool CheckHasFile(string fileName)
    {
        return File.Exists(AssetDirectoryPath + fileName);
    }

    void CacheFile(string fileName, byte[] data)
    {
        if (!Directory.Exists(AssetDirectoryPath))
            Directory.CreateDirectory(AssetDirectoryPath);

        FileStream cache = new System.IO.FileStream(AssetDirectoryPath + fileName, System.IO.FileMode.Create);
        cache.Write(data, 0, data.Length);
        cache.Close();

#if UNITY_IPHONE
        UnityEngine.iOS.Device.SetNoBackupFlag(AssetDirectoryPath + fileName);
#endif

        //Debug.Log("Cache saved: " + AssetDirectoryPath + fileName);
    }
}

