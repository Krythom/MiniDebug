using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniDebug.Util;
using Modding;
using UnityEngine;
using UnityEngine.EventSystems;

using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MiniDebug.Savestates;

public class SaveStateManager : MonoBehaviour
{
    private const int STATE_COUNT = 10;

    private MenuAction _currentMenu = MenuAction.None;
        
    private List<string> allStates = new(), curSelection = new();
    private string query = "";
    private int selector;
    private string lastSaveState;

    private static HeroController HC => HeroController.instance;
    private static GameManager GM => GameManager.instance;

    private static readonly MiniDebug MD = MiniDebug.Instance;

    public void LoadStateNames()
    {
        allStates.Clear();
        curSelection.Clear();
        query = "";
        selector = 0;
            
        allStates.AddRange(Directory.GetFiles($"{Application.persistentDataPath}/Savestates", "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension));
        allStates.Sort();
        curSelection = allStates;
    }

    public void LoadSaveState(bool duped)
    {
        _currentMenu = _currentMenu == MenuAction.None
            ? duped
                ? MenuAction.LoadStateDuped
                : MenuAction.LoadState
            : MenuAction.None;
            
        if (_currentMenu == MenuAction.None)
            return;
            
        if (GM.IsGamePaused())
        {
            MD.AcceptingInput = false;
            LoadStateNames();

            try
            {
                InputHandler ih = UIManager.instance.GetField<UIManager, InputHandler>("ih");
                ih.acceptingInput = false;
                EventSystem.current.sendNavigationEvents = false;
            }
            catch (Exception e)
            {
                MiniDebugMod.Instance.Log("Unable to disable UI input\n" + e.Message);
            }
        }
        else if (!string.IsNullOrEmpty(lastSaveState))
        {
            StartCoroutine(SaveData.LoadFromFile(BuildStatePath(lastSaveState))
                .LoadState(_currentMenu == MenuAction.LoadStateDuped));
            _currentMenu = MenuAction.None;
        }
        else
        {
            _currentMenu = MenuAction.None;
        }
    }

    public void SaveState(bool detailed)
    {
        string savestateDir = Path.Combine(Application.persistentDataPath, "Savestates");
        Directory.CreateDirectory(savestateDir);
        string loc = $"__TEMP_STATE_{GM.GetSceneNameString()}__{DateTimeString()}";

        if (File.Exists(Path.Combine(savestateDir, loc + ".json")))
        {
            for (int i = 0;; i++)
            {
                if (!File.Exists($"{loc}__{i}.json"))
                {
                    loc = $"{loc}__{i}";
                    break;
                }
            }
        }

        File.WriteAllText(
            BuildStatePath(loc), 
            JsonUtility.ToJson(SaveData.CreateSaveData(detailed), true)
        );
        lastSaveState = loc;
    }

    private void Update()
    {
        if (_currentMenu == MenuAction.None || GM.GetSceneNameString() == Constants.MENU_SCENE)
        {
            return;
        }
        if (!GM.IsGamePaused())
        {
            MD.AcceptingInput = true;
            _currentMenu = MenuAction.None;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (curSelection.Count == 0)
                return;
                
            lastSaveState = curSelection[selector];
            bool duped = _currentMenu == MenuAction.LoadStateDuped;
            CancelMenuInput();

            StartCoroutine(SaveData.LoadFromFile(BuildStatePath(lastSaveState)).LoadState(duped));
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelMenuInput();
        }
        else if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (query.Length == 0)
                return;
                
            query = query.Substring(0, query.Length - 1);
            UpdateSelection();
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            selector = selector == 0 ? curSelection.Count - 1 : selector - 1;
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            selector = selector == curSelection.Count - 1 ? 0 : selector + 1;
        }
        else if (Input.inputString.Length > 0)
        {
            query += Input.inputString;
            UpdateSelection();
        }
    }

    private void OnGUI()
    {
        if (_currentMenu == MenuAction.None)
        {
            return;
        }

        GUIHelper.Config cfg = GUIHelper.SaveConfig();
        bool wrap = GUI.skin.label.wordWrap;
        var clipping = GUI.skin.label.clipping;
            
        GUI.skin.label.wordWrap = false;
        GUI.skin.label.clipping = TextClipping.Overflow;
            
        GUI.Label(new Rect(0f, 300f, 200f, 200f), "Select a Save to Load From:");
        GUI.Label(new Rect(0f, 350f, 200f, 200f), ">" + query);

        int dispOffset = Math.Min(selector, Math.Max(0, curSelection.Count - STATE_COUNT));
        List<string> shownStates = curSelection.Count <= STATE_COUNT
            ? curSelection
            : curSelection.GetRange(dispOffset, STATE_COUNT);
        for (int i = 0; i < shownStates.Count; i++)
        {
            GUI.Label(new Rect(0, 375 + 25 * i, 200, 200), $"{(i == selector - dispOffset ? '*' : ' ')}{shownStates[i]}");
        }
            
        GUIHelper.RestoreConfig(cfg);
        GUI.skin.label.wordWrap = wrap;
        GUI.skin.label.clipping = clipping;
    }

    private static string DateTimeString()
    {
        DateTime now = DateTime.Now;
        return $"{now.Year}-{now.Month}-{now.Day}_{now.Hour}-{now.Minute}-{now.Second}";
    }

    private void UpdateSelection()
    {
        curSelection = allStates.FindAll(s => s.Contains(query));
        selector = 0;
    }

    private static string BuildStatePath(string name)
    {
        return Path.Combine(Application.persistentDataPath, Path.Combine("Savestates", $"{name}.json"));
    }

    private void CancelMenuInput()
    {
        MD.AcceptingInput = true;
        UIManager.instance.UIClosePauseMenu();
        GameCameras.instance.ResumeCameraShake();
        GM.actorSnapshotUnpaused.TransitionTo(0f);
        GM.isPaused = false;
        GM.ui.AudioGoToGameplay(.2f);
        HC.UnPause();
        Time.timeScale = 1f;
        _currentMenu = MenuAction.None;
        try
        {
            InputHandler ih = UIManager.instance.GetField<UIManager, InputHandler>("ih");
            ih.StartUIInput();
        }
        catch (Exception e)
        {
            MiniDebugMod.Instance.Log("Unable to restart UI input\n" + e.Message);
        }
    }

    private enum MenuAction
    {
        None,
        LoadState,
        LoadStateDuped
    }
}