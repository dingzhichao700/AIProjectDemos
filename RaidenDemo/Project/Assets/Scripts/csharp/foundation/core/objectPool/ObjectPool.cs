using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对象池
/// </summary>
/// <typeparam name="T"></typeparam>
public class ObjectPool<T> where T : class, new() {

    private readonly Stack<T> _pool = new Stack<T>();

    /**获取实例*/
    public T Get() {
        if (_pool.Count > 0) {
            return _pool.Pop();
        }
        return new T(); // 池子里没了，就新建
    }

    /**释放*/
    public void Release(T obj) {
        if (obj == null) return;
        _pool.Push(obj);
        if (_pool.Count > 500) {
            Debug.LogWarning("对象池" + typeof(T).FullName + "数量过多，请检查");
        }
    }

    public int Count => _pool.Count;
}
