using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class GameStateEditor : EditorWindow
{
    private string saveName = "Save";
    private List<string> savedStates = new List<string>();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Game State Manager")]
    public static void ShowWindow()
    {
        GetWindow<GameStateEditor>("Game State Manager");
    }

    private void OnEnable()
    {
        LoadSavedStates();
    }

    private void LoadSavedStates()
    {
        savedStates.Clear();
        string savedStatesStr = PlayerPrefs.GetString("SavedStates", "");
        if (!string.IsNullOrEmpty(savedStatesStr))
        {
            savedStates.AddRange(savedStatesStr.Split(','));
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Game State Manager", EditorStyles.boldLabel);
        DrawSaveSection();
        DrawLoadSection();
    }

    private void DrawSaveSection()
    {
        GUILayout.Space(10);
        GUILayout.Label("Save Current State", EditorStyles.boldLabel);
        saveName = EditorGUILayout.TextField("Save PlayerNameInputField", saveName);

        if (GUILayout.Button("Save State"))
        {
            string key = $"Save_{saveName}";
            FindFirstObjectByType<BoardManager>().SaveGameState(key);

            if (!savedStates.Contains(saveName))
            {
                savedStates.Add(saveName);
                PlayerPrefs.SetString("SavedStates", string.Join(",", savedStates));
                PlayerPrefs.Save();
            }
        }
    }

    private void DrawLoadSection()
    {
        GUILayout.Space(20);
        GUILayout.Label("Saved States", EditorStyles.boldLabel);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        foreach (var stateName in savedStates)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(stateName, GUILayout.Width(150));

            if (GUILayout.Button("Load"))
            {
                FindFirstObjectByType<BoardManager>().LoadGameState($"Save_{stateName}");
            }

            if (GUILayout.Button("Delete"))
            {
                PlayerPrefs.DeleteKey($"Save_{stateName}");
                savedStates.Remove(stateName);
                PlayerPrefs.SetString("SavedStates", string.Join(",", savedStates));
                PlayerPrefs.Save();
                GUIUtility.ExitGUI(); // Prevent layout issues
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
    }
}