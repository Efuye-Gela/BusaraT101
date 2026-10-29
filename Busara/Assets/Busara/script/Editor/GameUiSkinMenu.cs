using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Bakes the GameScene UI skin into the open scene, so the editor shows what play mode shows.</summary>
public static class GameUiSkinMenu
{
    [MenuItem("Tools/Busara/UI Skin/Apply Skin In Open Scene")]
    public static void ApplyInOpenScene()
    {
        GameUiSkinner skinner = Object.FindFirstObjectByType<GameUiSkinner>(FindObjectsInactive.Include);
        if (skinner == null || skinner.skin == null)
        {
            Debug.LogWarning("Open GameScene: its Canvas has a GameUiSkinner with a UI skin assigned.");
            return;
        }
        // Record everything the skinner may touch so the change can be undone and is saved with the scene.
        var touched = new List<Object>();
        foreach (Component component in skinner.GetComponentsInChildren<Component>(true))
            if (component is UnityEngine.UI.Graphic || component is RectTransform)
                touched.Add(component);
        Undo.RecordObjects(touched.ToArray(), "Apply UI skin");
        foreach (Object item in skinner.Apply())
        {
            if (item is GameObject created && !touched.Contains(created.transform))
                Undo.RegisterCreatedObjectUndo(created, "Apply UI skin");
            EditorUtility.SetDirty(item);
        }
        EditorSceneManager.MarkSceneDirty(skinner.gameObject.scene);
        Debug.Log($"Applied the {skinner.skin.displayName} UI skin. Save the scene to keep it.");
    }
}
