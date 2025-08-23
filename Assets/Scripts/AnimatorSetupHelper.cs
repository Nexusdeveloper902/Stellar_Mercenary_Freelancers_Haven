using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

#if UNITY_EDITOR
public class AnimatorSetupHelper : EditorWindow
{
    [MenuItem("Tools/Setup Player Animator")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorSetupHelper>("Animator Setup");
    }
    
    public AnimatorController animatorController;
    public TextAsset animationListJson;
    public string variant = "A";
    
    private void OnGUI()
    {
        GUILayout.Label("Animator Setup Helper", EditorStyles.boldLabel);
        
        animatorController = (AnimatorController)EditorGUILayout.ObjectField(
            "Animator Controller", animatorController, typeof(AnimatorController), false);
            
        animationListJson = (TextAsset)EditorGUILayout.ObjectField(
            "Animation List JSON", animationListJson, typeof(TextAsset), false);
            
        variant = EditorGUILayout.TextField("Variant (A, B, C, D, E)", variant);
        
        if (GUILayout.Button("Setup Basic States"))
        {
            SetupBasicStates();
        }
        
        if (GUILayout.Button("Setup All Animation States"))
        {
            SetupAllStates();
        }
    }
    
    private void SetupBasicStates()
    {
        if (animatorController == null)
        {
            Debug.LogError("Please assign an AnimatorController!");
            return;
        }
        
        // Basic animations that every character needs
        string[] basicAnimations = {
            "idle_down", "idle_up", "idle_side",
            "walk_down", "walk_up", "walk_side",
            "run_down", "run_up", "run_side",
            "jump_down", "jump_up", "jump_side"
        };
        
        foreach (string animName in basicAnimations)
        {
            CreateState(animName);
        }
        
        // Set idle_down as the default state
        var idleDownState = FindState("idle_down");
        if (idleDownState != null)
        {
            animatorController.layers[0].stateMachine.defaultState = idleDownState;
        }
        
        EditorUtility.SetDirty(animatorController);
        Debug.Log("Basic animator states created successfully!");
    }
    
    private void SetupAllStates()
    {
        if (animatorController == null || animationListJson == null)
        {
            Debug.LogError("Please assign both AnimatorController and Animation List JSON!");
            return;
        }
        
        // Parse JSON
        var list = JsonUtility.FromJson<AnimationListWrapper>(animationListJson.text);
        var relevantAnimations = new HashSet<string>();
        
        // Filter animations for the selected variant
        foreach (var anim in list.animations)
        {
            if (anim.path.Contains($"Variant {variant}/"))
            {
                relevantAnimations.Add(anim.name);
            }
        }
        
        // Create states for all relevant animations
        foreach (string animName in relevantAnimations)
        {
            CreateState(animName);
        }
        
        // Set idle_down as default if it exists
        var idleDownState = FindState("idle_down");
        if (idleDownState != null)
        {
            animatorController.layers[0].stateMachine.defaultState = idleDownState;
        }
        
        EditorUtility.SetDirty(animatorController);
        Debug.Log($"Created {relevantAnimations.Count} animator states for variant {variant}!");
    }
    
    private void CreateState(string stateName)
    {
        // Check if state already exists
        if (FindState(stateName) != null)
        {
            Debug.Log($"State '{stateName}' already exists, skipping...");
            return;
        }
        
        // Create new state
        var state = animatorController.layers[0].stateMachine.AddState(stateName);
        Debug.Log($"Created state: {stateName}");
    }
    
    private AnimatorState FindState(string stateName)
    {
        foreach (var state in animatorController.layers[0].stateMachine.states)
        {
            if (state.state.name == stateName)
                return state.state;
        }
        return null;
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
#endif