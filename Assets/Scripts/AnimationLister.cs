using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AnimationExporter
{
    [MenuItem("Tools/Export All Animation Clips to TXT")]
    private static void ExportToTxt()
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
        string path = EditorUtility.SaveFilePanel("Save Animation List as TXT", "", "AnimationList.txt", "txt");

        if (string.IsNullOrEmpty(path))
            return;

        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine($"Found {guids.Length} animation clips:\n");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip != null)
                {
                    writer.WriteLine($"{clip.name} ({assetPath})");
                }
            }
        }

        Debug.Log($"✅ Exported {guids.Length} animation clips to TXT at: {path}");
    }

    [MenuItem("Tools/Export All Animation Clips to JSON")]
    private static void ExportToJson()
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
        string path = EditorUtility.SaveFilePanel("Save Animation List as JSON", "", "AnimationList.json", "json");

        if (string.IsNullOrEmpty(path))
            return;

        List<AnimationData> list = new List<AnimationData>();
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip != null)
            {
                list.Add(new AnimationData { name = clip.name, path = assetPath });
            }
        }

        string json = JsonUtility.ToJson(new AnimationListWrapper { animations = list }, true);
        File.WriteAllText(path, json);

        Debug.Log($"✅ Exported {list.Count} animation clips to JSON at: {path}");
    }

    [System.Serializable]
    private class AnimationData
    {
        public string name;
        public string path;
    }

    [System.Serializable]
    private class AnimationListWrapper
    {
        public List<AnimationData> animations;
    }
}
