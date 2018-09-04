using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameItemPool : MonoBehaviour
{
    Dictionary<string, Stack<GameObject>> poolDict = new Dictionary<string, Stack<GameObject>>();

    public GameObject Spawn(GameObject itemPre, Vector3 pos)
    {
        GameObject item;
        Stack<GameObject> itemStack;
        poolDict.TryGetValue(itemPre.name, out itemStack);

        if (itemStack != null && itemStack.Count > 0)
        {
            item = itemStack.Pop();
        }
        else
        {
            item = Instantiate(itemPre);
            item.name = itemPre.name;
            item.transform.SetParent(transform, false);
        }

        item.transform.position = pos;
        item.transform.SetAsLastSibling();
        item.SetActive(true);

        return item;
    }

    public void Despawn(GameObject item)
    {
        if (!poolDict.ContainsKey(item.name))
            poolDict.Add(item.name, new Stack<GameObject>());
        poolDict[item.name].Push(item);

        item.SetActive(false);
    }
}
