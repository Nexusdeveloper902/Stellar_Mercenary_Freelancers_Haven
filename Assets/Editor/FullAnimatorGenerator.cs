// Place this script in a folder named "Editor" inside your Assets folder.
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;

public class FullAnimatorGenerator : EditorWindow
{
    private TextAsset _animationListJson;
    private string _baseVariant = "Variant A";
    private string _overrideVariant = "Variant C";
    private DefaultAsset _outputFolder;
    private Vector2 _scrollPosition;

    // Data structure for JSON parsing
    [System.Serializable]
    private class AnimationEntry { public string name; public string path; }
    [System.Serializable]
    private class AnimationList { public List<AnimationEntry> animations = new List<AnimationEntry>(); }

    [MenuItem("Tools/Animation/Full Animator Generator")]
    public static void ShowWindow()
    {
        GetWindow<FullAnimatorGenerator>("Animator Generator");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Animator Setup & Override Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This tool will create a complete Animator Controller and a variant-specific Override Controller based on your JSON file.", MessageType.Info);

        _animationListJson = (TextAsset)EditorGUILayout.ObjectField("Animation List JSON", _animationListJson, typeof(TextAsset), false);
        _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _outputFolder, typeof(DefaultAsset), false);

        _baseVariant = EditorGUILayout.TextField("Base Variant (e.g., Variant A)", _baseVariant);
        _overrideVariant = EditorGUILayout.TextField("Override Variant (e.g., Variant C)", _overrideVariant);

        if (GUILayout.Button("Generate Animator and Override"))
        {
            if (ValidateInputs())
            {
                GenerateAssets();
            }
        }
    }

    private bool ValidateInputs()
    {
        if (_animationListJson == null || _outputFolder == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign the JSON file and the Output Folder.", "OK");
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

    private void GenerateAssets()
    {
        string outputPath = AssetDatabase.GetAssetPath(_outputFolder);
        var baseAnimationMap = ParseAndFilterJson(_animationListJson.text, _baseVariant);

        // 1. Create the Base Animator Controller
        string controllerPath = Path.Combine(outputPath, $"{_baseVariant}_BaseController.controller");
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        // Add all necessary parameters based on the provided scripts
        AddParameters(controller);

        // Get the root state machine to add states
        var rootStateMachine = controller.layers[0].stateMachine;

        // Create Blend Trees and States
        var idleState = CreateBlendTreeState(controller, "Idle", "Horizontal", "Vertical", new List<string> { "idle_down", "idle_up", "idle_side" }, baseAnimationMap);
        var walkState = CreateBlendTreeState(controller, "Walk", "Horizontal", "Vertical", new List<string> { "walk_down", "walk_up", "walk_side" }, baseAnimationMap);
        var runState = CreateBlendTreeState(controller, "Run", "Horizontal", "Vertical", new List<string> { "run_down", "run_up", "run_side" }, baseAnimationMap);

        // All major states now use directional blend trees
        var jumpState = CreateBlendTreeState(controller, "Jump", "Horizontal", "Vertical", new List<string> { "jump_down", "jump_up", "jump_side" }, baseAnimationMap);
        var attackState = CreateBlendTreeState(controller, "Attack", "Horizontal", "Vertical", new List<string> { "sword_attack_down", "sword_attack_up", "sword_attack_side" }, baseAnimationMap);
        var duckState = CreateBlendTreeState(controller, "Duck", "Horizontal", "Vertical", new List<string> { "duck_down", "duck_up", "duck_side" }, baseAnimationMap);
        var slideState = CreateBlendTreeState(controller, "Slide", "Horizontal", "Vertical", new List<string> { "slide_down", "slide_up", "slide_side" }, baseAnimationMap);

        // Set Idle as the default state
        rootStateMachine.defaultState = idleState;

        // Create Transitions between states
        // Idle <-> Walk
        AddTransition(idleState, walkState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsMoving", threshold = 0f });
        AddTransition(walkState, idleState, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsMoving", threshold = 0f });

        // Walk <-> Run
        AddTransition(walkState, runState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsRunning", threshold = 0f });
        AddTransition(runState, walkState, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsRunning", threshold = 0f });

        // Any State -> Actions
        AddAnyStateTransition(rootStateMachine, jumpState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsJumping", threshold = 0f });
        AddTransition(jumpState, idleState, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsJumping", threshold = 0f });

        AddAnyStateTransition(rootStateMachine, attackState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsAttacking", threshold = 0f });
        AddTransition(attackState, idleState, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsAttacking", threshold = 0f });
        
        AddAnyStateTransition(rootStateMachine, duckState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsDucking", threshold = 0f });
        AddTransition(duckState, idleState, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsDucking", threshold = 0f });

        AddAnyStateTransition(rootStateMachine, slideState, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = "IsSliding", threshold = 0f });
        AddTransition(slideState, idleState, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = "IsSliding", threshold = 0f });


        Debug.Log($"Successfully created Base Controller at: {controllerPath}");

        // 2. Create the Animator Override Controller
        var overrideAnimationMap = ParseAndFilterJson(_animationListJson.text, _overrideVariant);
        AnimatorOverrideController overrideController = new AnimatorOverrideController(controller);
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

        foreach (var originalClip in controller.animationClips)
        {
            if (overrideAnimationMap.TryGetValue(originalClip.name, out string newClipPath))
            {
                AnimationClip newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newClipPath);
                if (newClip != null)
                {
                    overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(originalClip, newClip));
                }
            }
        }
        overrideController.ApplyOverrides(overrides);

        string overridePath = Path.Combine(outputPath, $"{_overrideVariant}_OverrideController.overrideController");
        AssetDatabase.CreateAsset(overrideController, overridePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Successfully created Override Controller at: {overridePath}");
        EditorUtility.DisplayDialog("Success", $"Successfully generated assets in {outputPath}", "OK");
    }

    private void AddParameters(AnimatorController controller)
    {
        controller.AddParameter("Horizontal", AnimatorControllerParameterType.Float);
        controller.AddParameter("Vertical", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsJumping", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDucking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSliding", AnimatorControllerParameterType.Bool);
    }
    
    private AnimatorState CreateSimpleState(AnimatorController controller, string name, string clipName, Dictionary<string, string> animMap)
    {
        var state = controller.layers[0].stateMachine.AddState(name);
        if (animMap.TryGetValue(clipName, out string path))
        {
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }
        return state;
    }

    private AnimatorState CreateBlendTreeState(AnimatorController controller, string name, string paramX, string paramY, List<string> clipNames, Dictionary<string, string> animMap)
    {
        BlendTree blendTree = new BlendTree
        {
            name = name + " Blend Tree",
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = paramX,
            blendParameterY = paramY
        };

        // Add clips to blend tree based on common naming conventions
        foreach (var clipName in clipNames)
        {
            if (animMap.TryGetValue(clipName, out string path))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    if (clipName.Contains("_down"))
                    {
                        blendTree.AddChild(clip, new Vector2(0, -1));
                    }
                    else if (clipName.Contains("_up"))
                    {
                        blendTree.AddChild(clip, new Vector2(0, 1));
                    }
                    else if (clipName.Contains("_side"))
                    {
                        // Add the single side animation to all horizontal and diagonal slots.
                        blendTree.AddChild(clip, new Vector2(1, 0));  // Right
                        blendTree.AddChild(clip, new Vector2(-1, 0)); // Left
                        blendTree.AddChild(clip, new Vector2(1, 1));   // Up-Right
                        blendTree.AddChild(clip, new Vector2(-1, 1));  // Up-Left
                        blendTree.AddChild(clip, new Vector2(1, -1));  // Down-Right
                        blendTree.AddChild(clip, new Vector2(-1, -1)); // Down-Left
                    }
                }
            }
        }

        var state = controller.layers[0].stateMachine.AddState(name);
        state.motion = blendTree;
        return state;
    }
    
    private void AddTransition(AnimatorState source, AnimatorState dest, AnimatorCondition condition)
    {
        var transition = source.AddTransition(dest);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
    }
    
    private void AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState dest, AnimatorCondition condition)
    {
        var transition = stateMachine.AddAnyStateTransition(dest);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
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
}