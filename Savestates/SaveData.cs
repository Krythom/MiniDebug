using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MiniDebug.Util;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;

using MDSer = MiniDebug.Util.Serialization;
using UObject = UnityEngine.Object;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MiniDebug.Savestates;

[Serializable]
public class SaveData : ISerializationCallbackReceiver
{
    public string Name;
    public Vector3 SavePos;
    public string SaveScene;
    public string SecondaryRoom;
    public SaveGameData Data;
    public Vector3 HazardRespawn;
    
    [NonSerialized] public MDSer.SerializableList<EnemyPosition> EnemyPositions = new();
    public List<string> BrokenBreakables = new();
    [NonSerialized] public MDSer.SerializableList<FsmState> FsmStates = new();
    [NonSerialized] public MDSer.SerializableList<ComponentActiveStatus> ComponentStatuses = new();

    [SerializeField] private string _EnemyPositionsRep;
    [SerializeField] private string _FsmStatesRep;
    [SerializeField] private string _ComponentStatusesRep;

    public void OnBeforeSerialize()
    {
        _EnemyPositionsRep = JsonUtility.ToJson(EnemyPositions);
        _FsmStatesRep = JsonUtility.ToJson(FsmStates);
        _ComponentStatusesRep = JsonUtility.ToJson(ComponentStatuses);
    }

    public void OnAfterDeserialize()
    {
        EnemyPositions = JsonUtility.FromJson<MDSer.SerializableList<EnemyPosition>>(_EnemyPositionsRep);
        FsmStates = JsonUtility.FromJson<MDSer.SerializableList<FsmState>>(_FsmStatesRep);
        ComponentStatuses =
            JsonUtility.FromJson<MDSer.SerializableList<ComponentActiveStatus>>(
                _ComponentStatusesRep);
    }

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
            SaveData data = new SaveData
            {
                Name = GM.GetSceneNameString(),
                SavePos = HC.gameObject.transform.position,
                SaveScene = GM.GetSceneNameString(),
                SecondaryRoom = GM.nextSceneName,
                Data = new SaveGameData(PD, SceneData.instance),
                HazardRespawn = PD.hazardRespawnLocation,
                EnemyPositions = new MDSer.SerializableList<EnemyPosition>(),
                BrokenBreakables = new List<string>(),
                FsmStates = new MDSer.SerializableList<FsmState>(),
                ComponentStatuses = new MDSer.SerializableList<ComponentActiveStatus>()
            };

            if (detailed)
            {
                HashSet<GameObject> processed = new();
                foreach (var meshRenderer in UObject.FindObjectsOfType<MeshRenderer>())
                {
                    if (processed.Contains(meshRenderer.gameObject)) continue;
                    processed.Add(meshRenderer.gameObject);

                    data.ComponentStatuses.Add(new ComponentActiveStatus
                    {
                        go = meshRenderer.gameObject.name,
                        type = meshRenderer.GetType().FullName,
                        enabled = meshRenderer.enabled
                    });
                }
                
                processed.Clear();
                foreach (var go in UObject.FindObjectsOfType<Collider2D>().Select(c2d => c2d.gameObject))
                {
                    if (processed.Contains(go)) continue;

                    if (go.scene.name is not null and not "DontDestroyOnLoad")
                    {
                        data.ComponentStatuses.AddRange(go.GetComponents<Collider2D>().Select(c2d => new ComponentActiveStatus
                        {
                            go = c2d.gameObject.name,
                            type = c2d.GetType().FullName,
                            enabled = c2d.enabled
                        }));
                    }

                    if (go.LocateMyFSM("health_manager_enemy"))
                    {
                        data.EnemyPositions.Add(new EnemyPosition
                        {
                            Name = go.name,
                            Pos = go.transform.position
                        });
                    }
                    else
                    {
                        var fsm = go.LocateMyFSM("FSM");
                        if (fsm != null && fsm.FsmEvents.Any(e => e.Name == "BREAKABLE DEACTIVE")
                                        && fsm.FsmVariables.BoolVariables.First(v => v.Name == "Activated").Value)
                        {
                            data.BrokenBreakables.Add(go.name + go.transform.position);
                        }
                    }
                    processed.Add(go);
                }

                foreach (var fsm in UObject.FindObjectsOfType<PlayMakerFSM>())
                {
                    if (fsm.gameObject.scene.name is null or "DontDestroyOnLoad")
                    {
                        continue;
                    }
                    try
                    {
                        data.FsmStates.Add(new FsmState
                        {
                            parentName = fsm.gameObject.name,
                            fsmName = fsm.FsmName,
                            stateName = fsm.ActiveStateName,
                            fsmFloats = FsmVariableHelper.ToDict<SerializableFloatDictionary, float>(fsm.FsmVariables.FloatVariables),
                            fsmInts = FsmVariableHelper.ToDict<SerializableIntDictionary, int>(fsm.FsmVariables.IntVariables),
                            fsmBools = FsmVariableHelper.ToDict<SerializableBoolDictionary, bool>(fsm.FsmVariables.BoolVariables),
                            fsmStrings = FsmVariableHelper.ToDict<SerializableStringDictionary, string>(fsm.FsmVariables.StringVariables)
                        });
                    }
                    catch (Exception e)
                    {
                        MiniDebugMod.Instance.Log($"Exception on {fsm.gameObject.name}-{fsm.FsmName}: {e}");
                        throw;
                    }
                }
            }

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

        if (EnemyPositions.Count > 0)
        {
            var gos = UObject.FindObjectsOfType<Collider2D>()
                .Select(c2d => c2d.gameObject)
                .Where(go => FSMUtility.LocateFSM(go, "health_manager_enemy"))
                .GroupBy(go => go.name)
                .ToDictionary(x => x.Key, x => x.ToList());
            foreach (var epos in EnemyPositions)
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

        if (ComponentStatuses.Count > 0)
        {
            yield return null;
            HashSet<string> types = new();
            Dictionary<string, List<bool>> statuses = new();
            foreach (var cs in ComponentStatuses)
            {
                types.Add(cs.type);
                string k = $"{cs.go}-{cs.type}";
                if (!statuses.ContainsKey(k))
                {
                    statuses.Add(k, new List<bool>());
                }
                statuses[k].Add(cs.enabled);
            }

            Assembly unityAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetType("UnityEngine.GameObject") != null);
            if (unityAssembly == null)
            {
                throw new Exception("Unable to find Unity assembly");
            }

            foreach (var component in types.Select(unityAssembly.GetType)
                         .SelectMany(UObject.FindObjectsOfType))
            {
                Component c = (Component) component;
                if (statuses.TryGetValue($"{c.gameObject.name}-{c.GetType().FullName}", out var l) && l.Count > 0)
                {
                    bool enabled = l[0];
                    l.RemoveAt(0);
                    switch (c)
                    {
                        case MeshRenderer mr:
                            mr.enabled = enabled;
                            break;
                        case Collider2D c2d:
                            c2d.enabled = enabled;
                            break;
                        default:
                            throw new Exception($"Unknown component type {c.GetType()}");
                    }
                }
            }
        }

        if (FsmStates.Count > 0)
        {
            Dictionary<string, List<FsmState>> states = new();
            foreach (var fsmState in FsmStates)
            {
                string k = fsmState.parentName + "-" + fsmState.fsmName;
                if (!states.ContainsKey(k))
                {
                    states.Add(k, new List<FsmState>());
                }
                states[k].Add(fsmState);
            }

            foreach (var fsm in UObject.FindObjectsOfType<PlayMakerFSM>())
            {
                if (states.TryGetValue(fsm.gameObject.name + '-' + fsm.FsmName, out var l) && l.Count > 0)
                {
                    FsmState s = l[0];
                    l.RemoveAt(0);
                    
                    FsmVariableHelper.FromDict(s.fsmFloats, fsm.FsmVariables);
                    FsmVariableHelper.FromDict(s.fsmInts, fsm.FsmVariables);
                    FsmVariableHelper.FromDict(s.fsmBools, fsm.FsmVariables);
                    FsmVariableHelper.FromDict(s.fsmStrings, fsm.FsmVariables);
                    
                    fsm.SetState(s.stateName);
                }
            }
        }
    }

    [Serializable]
    public class ComponentActiveStatus
    {
        public string go;
        public string type;
        public bool enabled;
    }
}