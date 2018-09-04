using System;

namespace UnityEngine.Events
{
    [Serializable] public class UnityBoolEvent : UnityEvent<bool> { }

    [Serializable] public class UnityIntEvent : UnityEvent<int> { }

    [Serializable] public class UnityFloatEvent : UnityEvent<float> { }

    [Serializable] public class UnityStringEvent : UnityEvent<string> { }
}