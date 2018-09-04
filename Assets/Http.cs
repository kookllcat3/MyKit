using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Http
{
    static int loadTimes;

    public static bool stopHttp;
    public static Action<bool> onShowLoading = delegate { };
    public static Action<string> onShowError = delegate { };

    public static IEnumerator Post(string url, WWWForm form, Action<string> onResponse, bool showLoading = false)
    {
        if (stopHttp)
            yield break;

        if (showLoading)
        {
            loadTimes++;
            onShowLoading(true);
        }

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            yield return www.SendWebRequest();

            if (www.isHttpError || www.isNetworkError)
            {
                //Debug.Log(www.error);
                onShowError("连线失败，请稍后再尝试！");
            }
            else if (www.responseCode == 200)
            {
                //Debug.Log("code:" + www.responseCode + "\n" + www.downloadHandler.text);
                if (onResponse != null)
                    onResponse(www.downloadHandler.text);
            }
            else
            {
                //Debug.Log("code:" + www.responseCode);
                onShowError("连线失败，请稍后再尝试！");
            }
        }

        if (showLoading)
        {
            loadTimes--;
            if (loadTimes <= 0)
            {
                loadTimes = 0;
                onShowLoading(false);
            }
        }
    }
}