﻿using System;
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
        private string lastSaveState = "quicksave";

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

        public void SaveState(bool detailed)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath + "/Savestates");
                string loc;

                if (GM.IsGamePaused())
                {
             
                    loc = $"{GM.GetSceneNameString()}__{DateTimeString()}";

                    if (File.Exists(loc + ".json"))
                    {
                        for (int i = 0; ; i++)
                        {
                            if (!File.Exists($"{loc}__{i}.json"))
                            {
                                loc = $"{loc}__{i}";
                                break;
                            }
                        }
                    }
                }
                else
                {
                    loc = $"quicksave";
                }


                List<EnemyPosition> enemyPositions = new();
                List<string> breakables = new();

                if (detailed)
                {
                    HashSet<GameObject> processed = new();
                    foreach (var go in FindObjectsOfType<Collider2D>().Select(c2d => c2d.gameObject))
                    {
                        if (go.LocateMyFSM("health_manager_enemy") && !processed.Contains(go))
                        {
                            processed.Add(go);
                            enemyPositions.Add(new EnemyPosition
                            {
                                Name = go.name,
                                Pos = go.transform.position
                            });
                        }
                        else if (!processed.Contains(go))
                        {
                            var fsm = go.LocateMyFSM("FSM");
                            if (fsm != null && fsm.FsmEvents.Any(e => e.Name == "BREAKABLE DEACTIVE")
                                            && fsm.FsmVariables.BoolVariables.First(v => v.Name == "Activated").Value)
                            {
                                processed.Add(go);
                                breakables.Add(go.name + go.transform.position);
                            }
                        }
                    }
                }

                SaveData data = new SaveData
                {
                    SavePos = HC.gameObject.transform.position,
                    Rooms = (from i in Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount) select UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name).ToArray<string>(),
                    Data = new SaveGameData(PD, SceneData.instance),
                    HazardRespawn = PD.hazardRespawnLocation,
                    EnemyPositions = EnemyPosition.serializeList(enemyPositions),
                    BrokenBreakables = breakables
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

            GM.ChangeToScene(save.Rooms[0], "", 0.4f);
            yield return new WaitUntil(() => GM.GetSceneNameString() == save.Rooms[0]);

            if (duped)
            {
                for (int i = 1; i < save.Rooms.Length; i++)
                {
                    USceneManager.LoadScene(save.Rooms[i], LoadSceneMode.Additive);
                    yield return null; // wait for LoadScene to complete
                }
            }

            GM.cameraCtrl.SetMode(CameraController.CameraMode.FOLLOWING);

            List<EnemyPosition> enemyPositions = EnemyPosition.deserializeList(save.EnemyPositions);
            if (enemyPositions.Count > 0)
            {
                var gos = FindObjectsOfType<Collider2D>()
                    .Select(c2d => c2d.gameObject)
                    .Where(go => FSMUtility.LocateFSM(go, "health_manager_enemy"))
                    .GroupBy(go => go.name)
                    .ToDictionary(x => x.Key, x => x.ToList());
                foreach (var epos in enemyPositions)
                {
                    if (!gos.ContainsKey(epos.Name) || gos[epos.Name].Count == 0)
                    {
                        MiniDebugMod.Instance.Log(
                            $"[WARNING] Couldn't find enemy \"{epos.Name}\" after loading savestate");
                        continue;
                    }

                    var go = gos[epos.Name][0];
                    go.transform.position = epos.Pos;
                    gos[epos.Name].RemoveAt(0);
                }
            }

            if (save.BrokenBreakables.Count > 0)
            {
                var breakables = new HashSet<string>(save.BrokenBreakables);
                foreach (var go in FindObjectsOfType<Collider2D>()
                             .Select(c2d => c2d.gameObject)
                             .Where(go =>
                             {
                                 var fsm = go.LocateMyFSM("FSM");
                                 return fsm != null && fsm.FsmEvents.Any(e => e.Name == "BREAKABLE DEACTIVE");
                             }))
                {
                    if (breakables.Contains(go.name + go.transform.position))
                    {
                        go.LocateMyFSM("FSM").SendEvent("BREAK");
                    }
                }
            }
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
            public Vector3 SavePos;
            public string[] Rooms;
            public SaveGameData Data;
            public Vector3 HazardRespawn;
            public string EnemyPositions;
            public List<string> BrokenBreakables;
        }

        private enum MenuAction
        {
            None,
            LoadState,
            LoadStateDuped
        }
    }
}
