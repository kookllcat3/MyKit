using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class HttpManager : MonoBehaviour
{
    #region Inspector

    public UnityBoolEvent onShowLoading;

    #endregion

    int loadTimes;

    public IEnumerator Post(string url, WWWForm form, Action<string> onResponse, Action<Dictionary<string, string>> onResponseHeaders, Dictionary<string, string> requestHeaders, bool showLoading = false)
    {
        if (showLoading)
        {
            loadTimes++;
            onShowLoading.Invoke(true);
        }

        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            if (requestHeaders != null)
            {
                foreach (var item in requestHeaders)
                {
                    www.SetRequestHeader(item.Key, item.Value);
                }
            }

            Debug.Log("send:" + url + "\n" + Encoding.UTF8.GetString(www.uploadHandler.data));

            yield return www.SendWebRequest();

            if (www.isHttpError || www.isNetworkError)
            {
                Debug.Log(www.error);
            }
            else if (www.responseCode == 200)
            {
                Debug.Log("code:" + www.responseCode + "\n" + www.downloadHandler.text);
                yield return new WaitForSeconds(1);
                if (onResponse != null)
                {
                    onResponse(www.downloadHandler.text);
                }
                if (onResponseHeaders != null)
                {
                    onResponseHeaders(www.GetResponseHeaders());
                }
            }
            else
            {
                Debug.Log("code:" + www.responseCode);
            }
        }

        if (showLoading)
        {
            loadTimes--;
            if (loadTimes <= 0)
            {
                loadTimes = 0;
                onShowLoading.Invoke(false);
            }
        }
    }
}
