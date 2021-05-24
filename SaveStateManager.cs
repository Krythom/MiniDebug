using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniDebug.Util;
using Modding;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MiniDebug
{
    public class SaveStateManager : MonoBehaviour
    {
        private const int STATE_COUNT = 10;

        private MenuAction _currentMenu = MenuAction.None;
        
        private List<string> allStates = new(), curSelection = new();
        private string query = "";
        private int selector;
        private string lastSaveState;

        private static PlayerData PD
        {
            get => PlayerData.instance;
            set => PlayerData.instance = value;
        }

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
                StartCoroutine(LoadState(_currentMenu == MenuAction.LoadStateDuped, lastSaveState));
                _currentMenu = MenuAction.None;
            }
            else
            {
                _currentMenu = MenuAction.None;
            }
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

                StartCoroutine(LoadState(duped, lastSaveState));
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

        public void SaveState()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath + "/Savestates");
                string loc = $"{GM.GetSceneNameString()}__{DateTimeString()}";

                if (File.Exists(loc + ".json"))
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

                SaveData data = new SaveData
                {
                    Name = GM.GetSceneNameString(),
                    SavePos = HC.gameObject.transform.position,
                    SaveScene = GM.GetSceneNameString(),
                    SecondaryRoom = GM.nextSceneName,
                    Data = new SaveGameData(PD, SceneData.instance),
                    HazardRespawn = PD.hazardRespawnLocation
                };

                data.Data.BeforeSave();

                File.WriteAllText(
                    $"{Application.persistentDataPath}/Savestates/{loc}.json", 
                    JsonUtility.ToJson(data, true)
                );
                lastSaveState = loc;
            }
            catch (Exception e)
            {
                MiniDebugMod.Instance.Log("Failed to save state\n" + e.Message);
            }
        }

        private IEnumerator LoadState(bool duped, string state)
        {
            SaveData save;
            string path = $"{Application.persistentDataPath}/Savestates/{state}.json";

            if (File.Exists(path))
            {
                save = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            }
            else
            {
                MiniDebugMod.Instance.Log($"Unable to load savestate {state}, file does not exist");
                yield break;
            }

            save.Data.AfterLoad();

            GM.ChangeToScene("Room_Sly_Storeroom", "", 0f);
            yield return new WaitUntil(() => GM.GetSceneNameString() == "Room_Sly_Storeroom");
            GM.ResetSemiPersistentItems();
            yield return null;

            PD = save.Data.playerData;
            PD.hazardRespawnLocation = save.HazardRespawn;

            GM.playerData = PD;
            HC.playerData = PD;
            HC.geoCounter.playerData = PD;
            HC.GetField<HeroController, HeroAnimationController>("animCtrl").SetField("pd", PD);

            SceneData.instance = GM.sceneData = save.Data.sceneData;

            HC.transform.position = save.SavePos;

            GM.cameraCtrl.SetField("isGameplayScene", true);

            HC.TakeHealth(1);
            HC.AddHealth(1);

            HC.geoCounter.geoTextMesh.text = PD.geo.ToString();

            HC.SetMPCharge(save.Data.playerData.MPCharge);
            if (save.Data.playerData.MPCharge == 0)
            {
                GameCameras.instance.soulOrbFSM.SendEvent("MP LOSE");
            }

            yield return null;
            GM.ChangeToScene(save.SaveScene, "", 0.4f);

            yield return new WaitUntil(() => GM.GetSceneNameString() == save.SaveScene);

            if (duped)
            {
                USceneManager.LoadScene(save.SecondaryRoom, LoadSceneMode.Additive);
            }

            GM.cameraCtrl.SetMode(CameraController.CameraMode.FOLLOWING);
        }

        private string DateTimeString()
        {
            DateTime now = DateTime.Now;
            return $"{now.Year}-{now.Month}-{now.Day}_{now.Hour}-{now.Minute}-{now.Second}";
        }

        private void UpdateSelection()
        {
            curSelection = allStates.FindAll(s => s.Contains(query));
            selector = 0;
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

        [Serializable]
        public class SaveData
        {
            public string Name;
            public Vector3 SavePos;
            public string SaveScene;
            public string SecondaryRoom;
            public SaveGameData Data;
            public Vector3 HazardRespawn;
        }

        private enum MenuAction
        {
            None,
            LoadState,
            LoadStateDuped
        }
    }
}
