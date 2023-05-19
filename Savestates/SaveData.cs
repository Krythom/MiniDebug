using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniDebug.Util;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;

using UObject = UnityEngine.Object;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MiniDebug.Savestates;

[Serializable]
public class SaveData
{
    public string Name;
    public Vector3 SavePos;
    public string SaveScene;
    public string SecondaryRoom;
    public SaveGameData Data;
    public Vector3 HazardRespawn;
    public string EnemyPositions;
    public List<string> BrokenBreakables;
    
    private static PlayerData PD
    {
        get => PlayerData.instance;
        set => PlayerData.instance = value;
    }

    private static HeroController HC => HeroController.instance;
    private static GameManager GM => GameManager.instance;
    
    public static SaveData CreateSaveData(bool detailed)
    {
        try
        {
            List<EnemyPosition> enemyPositions = new();
            List<string> breakables = new();

            if (detailed)
            {
                HashSet<GameObject> processed = new();
                foreach (var go in UObject.FindObjectsOfType<Collider2D>().Select(c2d => c2d.gameObject))
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
                Name = GM.GetSceneNameString(),
                SavePos = HC.gameObject.transform.position,
                SaveScene = GM.GetSceneNameString(),
                SecondaryRoom = GM.nextSceneName,
                Data = new SaveGameData(PD, SceneData.instance),
                HazardRespawn = PD.hazardRespawnLocation,
                EnemyPositions = EnemyPosition.serializeList(enemyPositions),
                BrokenBreakables = breakables
            };

            data.Data.BeforeSave();

            return data;
        }
        catch (Exception e)
        {
            MiniDebugMod.Instance.Log("Failed to save state\n" + e.Message);
            return null;
        }
    }

    public static SaveData LoadFromFile(string path)
    {
        if (File.Exists(path))
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }
        
        MiniDebugMod.Instance.Log($"Unable to load savestate at {path}, file does not exist");
        return null;
    }
    
    public IEnumerator LoadState(bool duped)
    {
        Data.AfterLoad();

        GM.ChangeToScene("Room_Sly_Storeroom", "", 0f);
        yield return new WaitUntil(() => GM.GetSceneNameString() == "Room_Sly_Storeroom");
        GM.ResetSemiPersistentItems();
        yield return null;

        PD = Data.playerData;
        PD.hazardRespawnLocation = HazardRespawn;

        GM.playerData = PD;
        HC.playerData = PD;
        HC.geoCounter.playerData = PD;
        HC.GetField<HeroController, HeroAnimationController>("animCtrl").SetField("pd", PD);

        SceneData.instance = GM.sceneData = Data.sceneData;

        HC.transform.position = SavePos;

        GM.cameraCtrl.SetField("isGameplayScene", true);

        HC.TakeHealth(1);
        HC.AddHealth(1);

        HC.geoCounter.geoTextMesh.text = PD.geo.ToString();

        HC.SetMPCharge(Data.playerData.MPCharge);
        if (Data.playerData.MPCharge == 0)
        {
            GameCameras.instance.soulOrbFSM.SendEvent("MP LOSE");
        }

        yield return null;
        GM.ChangeToScene(SaveScene, "", 0.4f);

        yield return new WaitUntil(() => GM.GetSceneNameString() == SaveScene);

        if (duped)
        {
            USceneManager.LoadScene(SecondaryRoom, LoadSceneMode.Additive);
            yield return null; // wait for LoadScene to complete
        }

        GM.cameraCtrl.SetMode(CameraController.CameraMode.FOLLOWING);

        List<EnemyPosition> enemyPositions = EnemyPosition.deserializeList(EnemyPositions);
        if (enemyPositions.Count > 0)
        {
            var gos = UObject.FindObjectsOfType<Collider2D>()
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

        if (BrokenBreakables.Count > 0)
        {
            var breakables = new HashSet<string>(BrokenBreakables);
            foreach (var go in UObject.FindObjectsOfType<Collider2D>()
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
}