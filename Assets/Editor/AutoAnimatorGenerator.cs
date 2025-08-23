// Place this script in a folder named "Editor" inside your Assets folder.
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class AutoAnimatorGenerator : EditorWindow
{
    // We define constants for the paths and variants to make the script cleaner and easier to modify.
    private const string JSON_PATH = "Assets/Data/animationList.json";
    private const string OUTPUT_FOLDER_PATH = "Assets/Animations/Generated";
    private const string BASE_VARIANT = "Variant A";
    private readonly string[] _variantsToGenerate = { "Variant A", "Variant B", "Variant C", "Variant D", "Variant E" };

    // Data structure for JSON parsing. This remains the same.
    [System.Serializable]
    private class AnimationEntry { public string name; public string path; }
    [System.Serializable]
    private class AnimationList { public List<AnimationEntry> animations = new List<AnimationEntry>(); }

    [MenuItem("Tools/Animation/Auto-Generate All Animators")]
    public static void ShowWindow()
    {
        // Show the editor window.
        GetWindow<AutoAnimatorGenerator>("Auto Animator Generator");
    }

    /// <summary>
    /// This method draws the UI for the editor window.
    /// </summary>
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Automatic Animator & Override Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This tool will automatically find the required files and generate a complete Animator Controller for the base variant and all specified override variants.\n\n" +
            $"JSON Source: '{JSON_PATH}'\n" +
            $"Output Folder: '{OUTPUT_FOLDER_PATH}'\n" +
            $"Base Variant: '{BASE_VARIANT}'\n" +
            $"Overrides: {string.Join(", ", _variantsToGenerate)}",
            MessageType.Info);

        // A single button to trigger the whole process.
        if (GUILayout.Button("Generate All Animator Assets"))
        {
            if (ValidateProjectState())
            {
                GenerateAllAssets();
            }
        }
    }

    /// <summary>
    /// Validates that the necessary source file and output folder exist before starting.
    /// </summary>
    private bool ValidateProjectState()
    {
        // Check if the source JSON file exists.
        if (!File.Exists(JSON_PATH))
        {
            EditorUtility.DisplayDialog("Error", $"The animation list JSON file could not be found at:\n{JSON_PATH}\n\nPlease make sure it exists.", "OK");
            return false;
        }

        // Check if the output folder exists. If not, create it.
        if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER_PATH))
        {
            Debug.Log($"Output folder not found. Creating it at: {OUTPUT_FOLDER_PATH}");
            // To create nested folders, we must create each directory level.
            string parentFolder = Path.GetDirectoryName(OUTPUT_FOLDER_PATH);
            string newFolderName = Path.GetFileName(OUTPUT_FOLDER_PATH);
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
            AssetDatabase.Refresh(); // Refresh to ensure the folder is recognized.
        }
        return true;
    }

    /// <summary>
    /// Main orchestration function that generates the base controller and all overrides.
    /// </summary>
    private void GenerateAllAssets()
    {
        // 1. Read the JSON file content once.
        string jsonContent = File.ReadAllText(JSON_PATH);
        if (string.IsNullOrEmpty(jsonContent))
        {
            EditorUtility.DisplayDialog("Error", "The JSON file is empty.", "OK");
            return;
        }

        // 2. Create the Base Animator Controller using the defined base variant.
        AnimatorController baseController = CreateBaseController(jsonContent);
        if (baseController == null)
        {
            EditorUtility.DisplayDialog("Error", "Failed to create the Base Animator Controller. Check the console for more details.", "OK");
            return;
        }

        // 3. Loop through all specified variants and generate an override controller for each.
        foreach (string variant in _variantsToGenerate)
        {
            GenerateOverrideController(baseController, jsonContent, variant);
        }

        // 4. Save all created assets and refresh the Asset Database.
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("--- Animator Generation Complete ---");
        EditorUtility.DisplayDialog("Success", $"Successfully generated all animator assets in\n{OUTPUT_FOLDER_PATH}", "OK");
    }

    /// <summary>
    /// Creates and configures the base Animator Controller from the JSON data for the base variant.
    /// </summary>
    private AnimatorController CreateBaseController(string json)
    {
        var baseAnimationMap = ParseAndFilterJson(json, BASE_VARIANT);
        if (baseAnimationMap.Count == 0)
        {
            Debug.LogError($"Failed to create base controller: No animations found for the base variant '{BASE_VARIANT}'. Please check your JSON file.");
            return null;
        }

        string controllerPath = Path.Combine(OUTPUT_FOLDER_PATH, $"{BASE_VARIANT}_BaseController.controller");
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        AddParameters(controller);
        var rootStateMachine = controller.layers[0].stateMachine;

        // Create all states using Blend Trees for directional movement.
        CreateBlendTreeState(controller, "Idle", "Horizontal", "Vertical", new List<string> { "idle_down", "idle_up", "idle_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Walk", "Horizontal", "Vertical", new List<string> { "walk_down", "walk_up", "walk_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Run", "Horizontal", "Vertical", new List<string> { "run_down", "run_up", "run_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Jump", "Horizontal", "Vertical", new List<string> { "jump_down", "jump_up", "jump_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Attack", "Horizontal", "Vertical", new List<string> { "sword_attack_down", "sword_attack_up", "sword_attack_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Duck", "Horizontal", "Vertical", new List<string> { "duck_down", "duck_up", "duck_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Slide", "Horizontal", "Vertical", new List<string> { "slide_down", "slide_up", "slide_side" }, baseAnimationMap);

        // Find the created states by name to set up transitions.
        var idleState = GetState(rootStateMachine, "Idle");
        var walkState = GetState(rootStateMachine, "Walk");
        var runState = GetState(rootStateMachine, "Run");
        var jumpState = GetState(rootStateMachine, "Jump");
        var attackState = GetState(rootStateMachine, "Attack");
        var duckState = GetState(rootStateMachine, "Duck");
        var slideState = GetState(rootStateMachine, "Slide");
        
        // Set Idle as the default entry state.
        rootStateMachine.defaultState = idleState;

        // --- Create Transitions between states ---
        // Idle <-> Walk
        AddTransition(idleState, walkState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsMoving" });
        AddTransition(walkState, idleState, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsMoving" });

        // Walk <-> Run
        AddTransition(walkState, runState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsRunning" });
        AddTransition(runState, walkState, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsRunning" });

        // Any State -> Actions (with an exit transition back to Idle)
        AddAnyStateTransition(rootStateMachine, jumpState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsJumping" });
        AddExitTransition(jumpState, idleState, hasExitTime: true);

        AddAnyStateTransition(rootStateMachine, attackState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsAttacking" });
        AddExitTransition(attackState, idleState, hasExitTime: true);
        
        AddAnyStateTransition(rootStateMachine, duckState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsDucking" });
        // Corrected line
        AddExitTransition(duckState, idleState, false, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsDucking" });

        AddAnyStateTransition(rootStateMachine, slideState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsSliding" });
        AddExitTransition(slideState, idleState, hasExitTime: true);

        Debug.Log($"Successfully created Base Controller at: {controllerPath}");
        return controller;
    }

    /// <summary>
    /// Generates a single Animator Override Controller for a given variant.
    /// </summary>
    private void GenerateOverrideController(AnimatorController baseController, string json, string variant)
    {
        var overrideAnimationMap = ParseAndFilterJson(json, variant);
        if (overrideAnimationMap.Count == 0)
        {
            Debug.LogWarning($"No animations found for variant '{variant}'. Override controller was not generated.");
            return;
        }

        AnimatorOverrideController overrideController = new AnimatorOverrideController(baseController);
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

        // Iterate through all clips in the base controller to find their replacements.
        foreach (var originalClip in baseController.animationClips)
        {
            if (overrideAnimationMap.TryGetValue(originalClip.name, out string newClipPath))
            {
                AnimationClip newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newClipPath);
                if (newClip != null)
                {
                    overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(originalClip, newClip));
                }
                else
                {
                    Debug.LogWarning($"Could not find animation clip at path for '{originalClip.name}': {newClipPath}");
                }
            }
        }
        
        overrideController.ApplyOverrides(overrides);

        string overridePath = Path.Combine(OUTPUT_FOLDER_PATH, $"{variant}_OverrideController.overrideController");
        AssetDatabase.CreateAsset(overrideController, overridePath);
        Debug.Log($"Successfully created Override Controller for '{variant}' at: {overridePath}");
    }

    #region Helper Methods

    /// <summary>
    /// Adds all necessary parameters to the Animator Controller.
    /// </summary>
    private void AddParameters(AnimatorController controller)
    {
        controller.AddParameter("Horizontal", AnimatorControllerParameterType.Float);
        controller.AddParameter("Vertical", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsJumping", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDucking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSliding", AnimatorControllerParameterType.Bool);
    }

    /// <summary>
    /// Creates a state machine state containing a 2D Simple Directional Blend Tree.
    /// </summary>
    private void CreateBlendTreeState(AnimatorController controller, string name, string paramX, string paramY, List<string> clipNames, Dictionary<string, string> animMap)
    {
        var state = controller.layers[0].stateMachine.AddState(name);
        
        BlendTree blendTree = new BlendTree
        {
            name = name + " Blend Tree",
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = paramX,
            blendParameterY = paramY,
            hideFlags = HideFlags.HideInHierarchy // Hides the blend tree asset from the project view to keep it clean.
        };
        
        AssetDatabase.AddObjectToAsset(blendTree, controller); // Embed the blend tree in the controller asset.

        // Add clips to blend tree based on common naming conventions.
        foreach (var clipName in clipNames)
        {
            if (animMap.TryGetValue(clipName, out string path))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    // This logic correctly maps a single "side" animation to all horizontal and diagonal directions.
                    if (clipName.Contains("_down")) blendTree.AddChild(clip, new Vector2(0, -1));
                    else if (clipName.Contains("_up")) blendTree.AddChild(clip, new Vector2(0, 1));
                    else if (clipName.Contains("_side"))
                    {
                        blendTree.AddChild(clip, new Vector2(1, 0));   // Right
                        blendTree.AddChild(clip, new Vector2(-1, 0));  // Left
                        blendTree.AddChild(clip, new Vector2(1, 1));   // Up-Right
                        blendTree.AddChild(clip, new Vector2(-1, 1));  // Up-Left
                        blendTree.AddChild(clip, new Vector2(1, -1));  // Down-Right
                        blendTree.AddChild(clip, new Vector2(-1, -1)); // Down-Left
                    }
                }
            }
        }
        state.motion = blendTree;
    }
    
    /// <summary>
    /// Adds a transition between two states with a single condition.
    /// </summary>
    private void AddTransition(AnimatorState source, AnimatorState dest, AnimatorCondition condition)
    {
        if (source == null || dest == null) return;
        var transition = source.AddTransition(dest);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
    }

    /// <summary>
    /// Adds a transition from a state back to a destination, typically used for actions returning to idle.
    /// Can be configured to use exit time or a specific condition.
    /// </summary>
    private void AddExitTransition(AnimatorState source, AnimatorState dest, bool hasExitTime = false, AnimatorCondition condition = null)
    {
        if (source == null || dest == null) return;
        var transition = source.AddTransition(dest);
        transition.hasExitTime = hasExitTime;
        transition.duration = 0.1f;
        if (hasExitTime) transition.exitTime = 0.9f; // End of the animation

        if (condition != null)
        {
            transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
        }
    }
    
    /// <summary>
    /// Adds a transition from the 'Any State' node to a destination state.
    /// </summary>
    private void AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState dest, AnimatorCondition condition)
    {
        if (dest == null) return;
        var transition = stateMachine.AddAnyStateTransition(dest);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.canTransitionToSelf = false; // Important for preventing loops.
        transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
    }

    /// <summary>
    /// Parses the JSON string and filters it to return a dictionary of animation names and paths for a specific variant.
    /// </summary>
    private Dictionary<string, string> ParseAndFilterJson(string json, string variant)
    {
        var map = new Dictionary<string, string>();
        var list = JsonUtility.FromJson<AnimationList>(json);
        string filter = $"/{variant}/"; // e.g., "/Variant C/"
        
        foreach (var entry in list.animations)
        {
            if (entry.path.Contains(filter) && !map.ContainsKey(entry.name))
            {
                map.Add(entry.name, entry.path);
            }
        }
        return map;
    }

    /// <summary>
    /// A helper to safely find a state in a state machine by name.
    /// </summary>
    private AnimatorState GetState(AnimatorStateMachine sm, string name)
    {
        return sm.states.FirstOrDefault(s => s.state.name == name).state;
    }

    // A simple struct to make transition definitions cleaner.
    private class AnimatorCondition
    {
        public AnimatorConditionMode mode;
        public float threshold;
        public string parameter;
    }

    #endregion
}
