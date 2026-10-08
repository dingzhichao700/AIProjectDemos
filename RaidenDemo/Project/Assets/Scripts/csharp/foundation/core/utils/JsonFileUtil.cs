using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class JsonFileUtil {

    public static T Load<T>(string path) where T : new() {
        try {
            if (!File.Exists(path))
                return new T();

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new T();

            //return JsonConvert.DeserializeObject<T>(json) ?? new T();
            try {
                var obj = JsonConvert.DeserializeObject<T>(json);
                return obj;
            } catch (Exception e) {
                Debug.LogError(e);

                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                Debug.Log("Dictionary Success");

                return new T();
            }
        } catch (Exception e) {
            Debug.LogError(e);
            return new T();
        }
    }

    public static void Save<T>(string path, T data) {
        try {
            string json = JsonConvert.SerializeObject(data,Formatting.Indented);
            File.WriteAllText(path, json);
        } catch (Exception e) {
            Debug.LogError(e);
        }
    }

}