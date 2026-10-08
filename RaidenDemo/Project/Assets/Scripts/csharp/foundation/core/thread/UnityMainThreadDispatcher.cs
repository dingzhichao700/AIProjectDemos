using System;
using UnityEngine;
using System.Collections.Generic;

public class UnityMainThreadDispatcher : MonoBehaviour {
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();

    public static UnityMainThreadDispatcher Instance { get; private set; }

    private void Awake() {
        Instance = this;
    }

    public void Update() {
        while (_executionQueue.Count > 0) {
            var action = _executionQueue.Dequeue();
            action?.Invoke();
        }
    }

    public void Enqueue(Action action) {
        lock (_executionQueue) {
            _executionQueue.Enqueue(action);
        }
    }
}
