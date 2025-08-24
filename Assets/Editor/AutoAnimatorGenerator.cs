// Place this script in a folder named "Editor" inside your Assets folder.
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class AutoAnimatorGenerator : EditorWindow
{
    // --- Configuration ---
    // (rest of the script is unchanged...)
    private const string JSON_PATH = "Assets/Data/animationList.json";
    private const string OUTPUT_FOLDER_PATH = "Assets/Animations/Generated";
    private const string BASE_VARIANT = "Variant A";
    private readonly string[] _variantsToGenerate = { "Variant A", "Variant B", "Variant C", "Variant D", "Variant E" };

    // (data structures are unchanged...)
    [System.Serializable]
    private class AnimationEntry { public string name; public string path; }
    [System.Serializable]
    private class AnimationList { public List<AnimationEntry> animations = new List<AnimationEntry>(); }
    
    private class AnimatorCondition
    {
        public AnimatorConditionMode mode;
        public float threshold;
        public string parameter;
    }

    [MenuItem("Tools/Animation/Auto-Generate All Animators")]
    public static void ShowWindow()
    {
        GetWindow<AutoAnimatorGenerator>("Auto Animator Generator");
    }

    // (GUI and most other methods are unchanged...)
    #region GUI
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

        if (GUILayout.Button("Generate All Animator Assets"))
        {
            if (ValidateProjectState())
            {
                GenerateAllAssets();
            }
        }
    }
    #endregion
    
    // (Core generation logic is unchanged...)
    #region Core Generation Logic
    /// <summary>
    /// Validates that the necessary source file and output folder exist before starting.
    /// </summary>
    private bool ValidateProjectState()
    {
        if (!File.Exists(JSON_PATH))
        {
            EditorUtility.DisplayDialog("Error", $"The animation list JSON file could not be found at:\n{JSON_PATH}\n\nPlease make sure it exists.", "OK");
            return false;
        }

        if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER_PATH))
        {
            Debug.Log($"Output folder not found. Creating it at: {OUTPUT_FOLDER_PATH}");
            string parentFolder = Path.GetDirectoryName(OUTPUT_FOLDER_PATH);
            string newFolderName = Path.GetFileName(OUTPUT_FOLDER_PATH);
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
            AssetDatabase.Refresh();
        }
        return true;
    }

    /// <summary>
    /// Main orchestration function that generates the base controller and all overrides.
    /// </summary>
    private void GenerateAllAssets()
    {
        string jsonContent = File.ReadAllText(JSON_PATH);
        if (string.IsNullOrEmpty(jsonContent))
        {
            EditorUtility.DisplayDialog("Error", "The JSON file is empty.", "OK");
            return;
        }

        AnimatorController baseController = CreateBaseController(jsonContent);
        if (baseController == null)
        {
            EditorUtility.DisplayDialog("Error", "Failed to create the Base Animator Controller. Check the console for more details.", "OK");
            return;
        }

        foreach (string variant in _variantsToGenerate)
        {
            GenerateOverrideController(baseController, jsonContent, variant);
        }

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

        // Create all states using Blend Trees. The "Slide" state will now get the event added automatically.
        CreateBlendTreeState(controller, "Idle", new List<string> { "idle_down", "idle_up", "idle_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Walk", new List<string> { "walk_down", "walk_up", "walk_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Run", new List<string> { "run_down", "run_up", "run_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Jump", new List<string> { "jump_down", "jump_up", "jump_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Attack", new List<string> { "sword_attack_down", "sword_attack_up", "sword_attack_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Duck", new List<string> { "duck_down", "duck_up", "duck_side" }, baseAnimationMap);
        CreateBlendTreeState(controller, "Slide", new List<string> { "slide_down", "slide_up", "slide_side" }, baseAnimationMap);

        // Find the created states by name to set up transitions.
        var idleState = GetState(rootStateMachine, "Idle");
        var walkState = GetState(rootStateMachine, "Walk");
        var runState = GetState(rootStateMachine, "Run");
        var jumpState = GetState(rootStateMachine, "Jump");
        var attackState = GetState(rootStateMachine, "Attack");
        var duckState = GetState(rootStateMachine, "Duck");
        var slideState = GetState(rootStateMachine, "Slide");
        
        rootStateMachine.defaultState = idleState;

        // --- Create Transitions ---
        AddTransition(idleState, walkState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsMoving" });
        AddTransition(walkState, idleState, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsMoving" });
        AddTransition(walkState, runState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsRunning" });
        AddTransition(runState, walkState, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsRunning" });

        // Any State -> Actions
        AddAnyStateTransition(rootStateMachine, jumpState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsJumping" });
        AddAnyStateTransition(rootStateMachine, attackState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsAttacking" });
        AddAnyStateTransition(rootStateMachine, duckState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsDucking" });
        AddAnyStateTransition(rootStateMachine, slideState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsSliding" });

        // Exit Transitions FROM Actions back to Idle
        AddExitTransition(jumpState, idleState, hasExitTime: true, condition: new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsJumping" });
        AddExitTransition(attackState, idleState, hasExitTime: true, condition: new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsAttacking" });
        AddExitTransition(duckState, idleState, condition: new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsDucking" });
        AddExitTransition(slideState, idleState, hasExitTime: false, condition: new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsSliding" });

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

        foreach (var originalClip in baseController.animationClips)
        {
            if (overrideAnimationMap.TryGetValue(originalClip.name, out string newClipPath))
            {
                AnimationClip newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newClipPath);
                if (newClip != null)
                {
                    // *** NEW FEATURE: If the original clip had our special event, add it to the override clip too! ***
                    if (AnimationUtility.GetAnimationEvents(originalClip).Any(e => e.functionName == "OnSlideAnimationEnd"))
                    {
                        AddEndEventToClip(newClip, "OnSlideAnimationEnd");
                    }
                    overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(originalClip, newClip));
                }
            }
        }
        
        overrideController.ApplyOverrides(overrides);

        string overridePath = Path.Combine(OUTPUT_FOLDER_PATH, $"{variant}_OverrideController.overrideController");
        AssetDatabase.CreateAsset(overrideController, overridePath);
        Debug.Log($"Successfully created Override Controller for '{variant}' at: {overridePath}");
    }
    #endregion

    #region Helper Methods

    // --- THIS IS THE FIX ---
    /// <summary>
    /// Adds all necessary parameters to the Animator Controller.
    /// </summary>
    private void AddParameters(AnimatorController controller)
    {
        // Blend Tree Parameters
        controller.AddParameter("Horizontal", AnimatorControllerParameterType.Float);
        controller.AddParameter("Vertical", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float); // ADDED
        
        // State Parameters
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsJumping", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDucking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSliding", AnimatorControllerParameterType.Bool);

        // Trigger Parameters
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger); // ADDED
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger); // ADDED
        controller.AddParameter("Slide", AnimatorControllerParameterType.Trigger); // ADDED
    }

    // (The rest of the script is unchanged...)
    // ...
    private void CreateBlendTreeState(AnimatorController controller, string name, List<string> clipNames, Dictionary<string, string> animMap)
    {
        var state = controller.layers[0].stateMachine.AddState(name);
        
        BlendTree blendTree = new BlendTree
        {
            name = name + " Blend Tree",
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "Horizontal",
            blendParameterY = "Vertical",
            hideFlags = HideFlags.HideInHierarchy
        };
        
        AssetDatabase.AddObjectToAsset(blendTree, controller);

        foreach (var clipName in clipNames)
        {
            if (animMap.TryGetValue(clipName, out string path))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    // *** NEW FEATURE: If this is the slide state, inject the animation event. ***
                    if (name == "Slide")
                    {
                        AddEndEventToClip(clip, "OnSlideAnimationEnd");
                    }

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
    /// Adds an AnimationEvent to the very end of an AnimationClip.
    /// Checks for duplicates to prevent adding the same event multiple times.
    /// </summary>
    private void AddEndEventToClip(AnimationClip clip, string functionName)
    {
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        bool eventExists = events.Any(e => e.functionName == functionName && Mathf.Approximately(e.time, clip.length));

        if (!eventExists)
        {
            AnimationEvent newEvent = new AnimationEvent
            {
                time = clip.length,
                functionName = functionName,
            };
            List<AnimationEvent> eventList = new List<AnimationEvent>(events);
            eventList.Add(newEvent);
            AnimationUtility.SetAnimationEvents(clip, eventList.ToArray());
            EditorUtility.SetDirty(clip);
            Debug.Log($"Added event '{functionName}' to clip '{clip.name}'");
        }
    }

    private void AddTransition(AnimatorState source, AnimatorState dest, AnimatorCondition condition)
    {
        if (source == null || dest == null) return;
        var transition = source.AddTransition(dest);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
    }

    private void AddExitTransition(AnimatorState source, AnimatorState dest, bool hasExitTime = false, AnimatorCondition condition = null)
    {
        if (source == null || dest == null) return;
        var transition = source.AddTransition(dest);
        transition.hasExitTime = hasExitTime;
        transition.duration = 0.1f;
        if (hasExitTime) transition.exitTime = 1f;

        if (condition != null)
        {
            transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
        }
    }
    
    private void AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState dest, AnimatorCondition condition)
    {
        if (dest == null) return;
        var transition = stateMachine.AddAnyStateTransition(dest);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
    }

    private Dictionary<string, string> ParseAndFilterJson(string json, string variant)
    {
        var map = new Dictionary<string, string>();
        var list = JsonUtility.FromJson<AnimationList>(json);
        string filter = $"/{variant}/";
        
        foreach (var entry in list.animations)
        {
            if (entry.path.Contains(filter) && !map.ContainsKey(entry.name))
            {
                map.Add(entry.name, entry.path);
            }
        }
        return map;
    }

    private AnimatorState GetState(AnimatorStateMachine sm, string name)
    {
        foreach (var childState in sm.states)
        {
            if (childState.state.name == name)
            {
                return childState.state;
            }
        }
        return null;
    }

    #endregion
}