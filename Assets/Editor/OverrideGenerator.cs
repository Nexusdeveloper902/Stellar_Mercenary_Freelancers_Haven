// Place this script in a folder named "Editor" inside your Assets folder.
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;

public class OverrideGenerator : EditorWindow
{
    private AnimatorController _baseController;
    private TextAsset _animationListJson;
    private string _overrideVariant = "Variant C";
    private DefaultAsset _outputFolder;

    // Data structure for JSON parsing
    [System.Serializable]
    private class AnimationEntry { public string name; public string path; }
    [System.Serializable]
    private class AnimationList { public List<AnimationEntry> animations = new List<AnimationEntry>(); }

    [MenuItem("Tools/Animation/Override Generator")]
    public static void ShowWindow()
    {
        GetWindow<OverrideGenerator>("Override Generator");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Animator Override Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This tool generates a variant-specific Override Controller from an existing Base Controller and your JSON file.", MessageType.Info);

        _baseController = (AnimatorController)EditorGUILayout.ObjectField("Base Animator Controller", _baseController, typeof(AnimatorController), false);
        _animationListJson = (TextAsset)EditorGUILayout.ObjectField("Animation List JSON", _animationListJson, typeof(TextAsset), false);
        _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _outputFolder, typeof(DefaultAsset), false);

        _overrideVariant = EditorGUILayout.TextField("Override Variant (e.g., Variant C)", _overrideVariant);

        if (GUILayout.Button("Generate Override Controller"))
        {
            if (ValidateInputs())
            {
                GenerateOverride();
            }
        }
    }

    private bool ValidateInputs()
    {
        if (_baseController == null || _animationListJson == null || _outputFolder == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign the Base Controller, JSON file, and the Output Folder.", "OK");
            return false;
        }
        if (string.IsNullOrWhiteSpace(_overrideVariant))
        {
            EditorUtility.DisplayDialog("Error", "The Override Variant cannot be empty.", "OK");
            return false;
        }
        string path = AssetDatabase.GetAssetPath(_outputFolder);
        if (!AssetDatabase.IsValidFolder(path))
        {
            EditorUtility.DisplayDialog("Error", "The selected Output Folder is not a valid folder.", "OK");
            return false;
        }
        return true;
    }

    private void GenerateOverride()
    {
        string outputPath = AssetDatabase.GetAssetPath(_outputFolder);
        
        // Parse the JSON to get the animation clips for the specified override variant
        var overrideAnimationMap = ParseAndFilterJson(_animationListJson.text, _overrideVariant);

        if (overrideAnimationMap.Count == 0)
        {
            Debug.LogWarning($"No animations found for variant '{_overrideVariant}'. Override controller was not generated.");
            EditorUtility.DisplayDialog("Warning", $"No animations were found for the variant '{_overrideVariant}'. Please check your JSON file and variant name.", "OK");
            return;
        }

        // Create the Animator Override Controller, linking it to the base controller
        AnimatorOverrideController overrideController = new AnimatorOverrideController(_baseController);
        
        // Prepare a list to hold the clip override pairs
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

        // Iterate through all clips in the base controller to find their replacements
        foreach (var originalClip in _baseController.animationClips)
        {
            // Check if our parsed map has a replacement for the current original clip
            if (overrideAnimationMap.TryGetValue(originalClip.name, out string newClipPath))
            {
                AnimationClip newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newClipPath);
                if (newClip != null)
                {
                    // Add the pair (original, new) to our list
                    overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(originalClip, newClip));
                }
                else
                {
                    Debug.LogWarning($"Could not find animation clip at path for '{originalClip.name}': {newClipPath}");
                }
            }
        }
        
        // Apply all the collected overrides to the controller
        overrideController.ApplyOverrides(overrides);

        // Create the .overrideController asset file
        string overridePath = Path.Combine(outputPath, $"{_overrideVariant}_OverrideController.overrideController");
        AssetDatabase.CreateAsset(overrideController, overridePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Successfully created Override Controller at: {overridePath}");
        EditorUtility.DisplayDialog("Success", $"Successfully generated override controller for '{_overrideVariant}' in {outputPath}", "OK");
    }

    private Dictionary<string, string> ParseAndFilterJson(string json, string variant)
    {
        var map = new Dictionary<string, string>();
        var list = JsonUtility.FromJson<AnimationList>(json);
        string filter = $"/{variant}/"; // e.g. "/Variant C/"
        
        foreach (var entry in list.animations)
        {
            if (entry.path.Contains(filter) && !map.ContainsKey(entry.name))
            {
                map.Add(entry.name, entry.path);
            }
        }
        return map;
    }
}