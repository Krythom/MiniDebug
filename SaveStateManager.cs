using System;
using System.Collections;
using System.IO;
using MiniDebug.Util;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;

using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MiniDebug
{
    public class SaveStateManager : MonoBehaviour
    {
        private const int STATE_COUNT = 10;

        private MenuAction _currentMenu = MenuAction.None;
        private string[] saveNames = new string[STATE_COUNT];

        private static PlayerData PD
        {
            get => PlayerData.instance;
            set => PlayerData.instance = value;
        }

        private static HeroController HC => HeroController.instance;
        private static GameManager GM => GameManager.instance;

        public void LoadStateNames()
        {
            for (int i = 0; i < STATE_COUNT; i++)
            {
                string path = GetStateFileName(i);
                if (File.Exists(path))
                {
                    SaveData saveData = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                    saveNames[i] = saveData.Name;
                }
                else
                {
                    saveNames[i] = "Empty Slot";
                }
            }
        }

        public void CreateSaveState()
            => _currentMenu = _currentMenu == MenuAction.None
                ? MenuAction.SaveState
                : MenuAction.None;

        public void LoadSaveState(bool duped)
            => _currentMenu = _currentMenu == MenuAction.None
                ? duped
                    ? MenuAction.LoadStateDuped
                    : MenuAction.LoadState
                : MenuAction.None;

        private void Update()
        {
            if (GM.GetSceneNameString() == Constants.MENU_SCENE || _currentMenu == MenuAction.None)
            {
                _currentMenu = MenuAction.None;
                return;
            }

            for (KeyCode key = KeyCode.Alpha0; key <= KeyCode.Alpha9; key++)
            {
                if (!Input.GetKeyDown(key))
                {
                    continue;
                }

                if (_currentMenu == MenuAction.LoadState || _currentMenu == MenuAction.LoadStateDuped)
                {
                    StartCoroutine(LoadState(_currentMenu == MenuAction.LoadStateDuped, key - KeyCode.Alpha0));
                }
                else
                {
                    SaveState(key - KeyCode.Alpha0);
                }

                _currentMenu = MenuAction.None;
                break;
            }
        }

        private void OnGUI()
        {
            if (_currentMenu == MenuAction.None)
            {
                return;
            }

            GUIHelper.Config cfg = GUIHelper.SaveConfig();

            GUI.Label(new Rect(0f, 300f, 200f, 200f), "Select Which Slot To Save To/Load From:");
            for (int i = 0; i < STATE_COUNT; i++)
            {
                GUI.Label(new Rect(0, 350 + 25 * i, 200, 200), $"Slot {i} - {saveNames[i]}");
            }

            GUIHelper.RestoreConfig(cfg);
        }

        private void SaveState(int num)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath + "/Savestates");

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
                    GetStateFileName(num), 
                    JsonUtility.ToJson(data, true)
                );

                saveNames[num] = GM.GetSceneNameString();
            }
            catch (Exception e)
            {
                MiniDebugMod.Instance.Log("Failed to save state\n" + e.Message);
            }
        }

        private IEnumerator LoadState(bool duped, int num)
        {
            SaveData save;
            string path = GetStateFileName(num);

            if (File.Exists(path))
            {
                save = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            }
            else
            {
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

        private string GetStateFileName(int num)
            => $"{Application.persistentDataPath}/Savestates/savestate{num}.json";

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
            SaveState,
            LoadState,
            LoadStateDuped
        }
    }
}
