using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;


public class WebInputField : MonoBehaviour, IPointerClickHandler
{
    [DllImport("__Internal")]
    static extern void InputBox(string msg, string callbackObj, string callbackFunc);

    InputField inputField;

    void Start()
    {
        inputField = GetComponent<InputField>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
#if UNITY_WEBGL && !UNITY_EDITOR    
        InputBox(inputField.text, gameObject.name, "Callback");
#endif
    }


#if UNITY_WEBGL && !UNITY_EDITOR
    void Callback(string str)
    {
        inputField.text = str;
    }
#endif
}
