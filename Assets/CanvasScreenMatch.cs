using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasScreenMatch : MonoBehaviour
{
    void Awake()
    {
        var canvas = GetComponent<CanvasScaler>();
        Vector2 size = new Vector2(Screen.width / canvas.referenceResolution.x, Screen.height / canvas.referenceResolution.y);
        canvas.matchWidthOrHeight = size.y > size.x ? 0 : 1;
    }
}