using System.Collections.Generic;
using UnityEngine;

public class MonoBehaviourObjectPool<T> : MonoBehaviour where T : MonoBehaviour {

    private List<T> pool = new List<T>(); // 存储池中的 MonoBehaviour 对象

    void Awake() {
    }

    // 获取一个 MonoBehaviour 对象
    public T GetObject() {
        T obj;
        if (pool.Count > 0) {
            obj = pool[0]; // 从池中取出第一个对象
            pool.RemoveAt(0);
        } else {
            obj = CreateNewObject(); // 如果池为空，创建一个新的对象
        }
        obj.gameObject.SetActive(true); // 激活对象
        return obj;
    }

    // 将对象放回池中
    public void ReturnObject(T obj) {
        obj.gameObject.SetActive(false); // 禁用对象
        pool.Add(obj); // 将对象放回池中
    }

    // 创建一个新的 MonoBehaviour 对象
    private T CreateNewObject() {
        GameObject newObj = new GameObject(typeof(T).Name, typeof(RectTransform)); // 创建一个新的 GameObject
        T newComponent = newObj.AddComponent<T>(); // 添加 MonoBehaviour 脚本
        return newComponent;
    }
}
