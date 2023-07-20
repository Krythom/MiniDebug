using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GlobalEnums;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
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
    public float KnightGravityScale = 1f;
    public Vector2 KnightVelocity;
    public string KnightTransformParent;
    
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
            GameObject knight = HC.gameObject;
            SaveData data = new SaveData
            {
                Name = GM.GetSceneNameString(),
                SavePos = HC.gameObject.transform.position,
                SaveScene = GM.GetSceneNameString(),
                SecondaryRoom = GM.nextSceneName,
                Data = new SaveGameData(PD, SceneData.instance),
                HazardRespawn = PD.hazardRespawnLocation,
                KnightGravityScale = knight.GetComponent<Rigidbody2D>().gravityScale,
                KnightVelocity = knight.GetComponent<Rigidbody2D>().velocity,
                KnightTransformParent = knight.transform.parent == null ? "" : HC.gameObject.transform.parent.gameObject.name,
                EnemyPositions = new MDSer.SerializableList<EnemyPosition>(),
                BrokenBreakables = new List<string>(),
                FsmStates = new MDSer.SerializableList<FsmState>(),
                ComponentStatuses = new MDSer.SerializableList<ComponentActiveStatus>()
            };

            if (detailed)
            {
                data.AddComponentStatuses();

                HashSet<GameObject> processedGos = new();
                foreach (var go in UObject.FindObjectsOfType<Collider2D>().Select(c2d => c2d.gameObject))
                {
                    if (processedGos.Contains(go)) continue;

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
                    processedGos.Add(go);
                }

                foreach (var fsm in UObject.FindObjectsOfType<PlayMakerFSM>())
                {
                    if (fsm.gameObject.scene.name is null or "DontDestroyOnLoad") continue;
                    
                    try
                    {
                        FsmState state = new FsmState
                        {
                            parentName = fsm.gameObject.name,
                            fsmName = fsm.FsmName,
                            stateName = fsm.ActiveStateName,
                            fsmFloats = FsmVariableHelper.ToDict<SerializableFloatDictionary, float>(fsm.FsmVariables
                                .FloatVariables),
                            fsmInts = FsmVariableHelper.ToDict<SerializableIntDictionary, int>(fsm.FsmVariables
                                .IntVariables),
                            fsmBools = FsmVariableHelper.ToDict<SerializableBoolDictionary, bool>(fsm.FsmVariables
                                .BoolVariables),
                            fsmStrings =
                                FsmVariableHelper.ToDict<SerializableStringDictionary, string>(fsm.FsmVariables
                                    .StringVariables)
                        };

                        var wait = fsm.Fsm.ActiveState.Actions.FirstOrDefault(a => a is Wait or WaitRandom);
                        if (wait != null)
                        {
                            state.waitRealTime = ((FsmBool)wait.GetType().GetField("realTime")!.GetValue(wait)).Value;
                            state.waitTimer = ((FsmFloat)wait.GetType()
                                .GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(wait)).Value;
                        }
                        
                        data.FsmStates.Add(state);
                    }
                    catch (Exception e)
                    {
                        MiniDebugMod.Instance.Log(
                            $"[WARNING] Exception storing FSM state for {fsm.gameObject.name}-{fsm.FsmName}::{fsm.ActiveStateName}: {e}");
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

    private void AddComponentStatuses()
    {
        HashSet<Component> processedComponents = new();

        bool testAndAdd(Component c)
        {
            if (c == null || processedComponents.Contains(c)) return false;
            processedComponents.Add(c);
            return true;
        }
        foreach (var action in UObject.FindObjectsOfType<PlayMakerFSM>()
                     .Where(fsm => fsm.gameObject.scene.name is not null and not "DontDestroyOnLoad")
                     .SelectMany(fsm => fsm.FsmStates)
                     .SelectMany(s => s.Actions))
        {
            var fsm = action.Fsm;
            switch (action)
            {
                case SetCollider sbc:
                    var sbcComponent = GetOwnerComponent<BoxCollider2D>(fsm, sbc.gameObject);
                    if (!testAndAdd(sbcComponent)) continue;
                    this.ComponentStatuses.Add(new ComponentActiveStatus(sbcComponent, sbcComponent.enabled));
                    break;
                case SetCircleCollider scc:
                    var sccComponent = GetOwnerComponent<CircleCollider2D>(fsm, scc.gameObject);
                    if (!testAndAdd(sccComponent)) continue;
                    this.ComponentStatuses.Add(new ComponentActiveStatus(sccComponent, sccComponent.enabled));
                    break;
                case SetPolygonCollider spc:
                    var spcComponent = GetOwnerComponent<PolygonCollider2D>(fsm, spc.gameObject);
                    if (!testAndAdd(spcComponent)) continue;
                    this.ComponentStatuses.Add(new ComponentActiveStatus(spcComponent, spcComponent.enabled));
                    break;
                case SetMeshRenderer smr:
                    var smrComponent = GetOwnerComponent<MeshRenderer>(fsm, smr.gameObject);
                    if (!testAndAdd(smrComponent)) continue;
                    this.ComponentStatuses.Add(new ComponentActiveStatus(smrComponent, smrComponent.enabled));
                    break;
            }
        }
    }

    private static T GetOwnerComponent<T>(Fsm fsm, FsmOwnerDefault owner) where T : class
    {
        var go = fsm.GetOwnerDefaultTarget(owner);
        return go == null ? null : go.GetComponent<T>();
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
        if (HC.transitionState != HeroTransitionState.WAITING_TO_TRANSITION)
        {
            float t = 0;
            while (HC.transitionState != HeroTransitionState.WAITING_TO_TRANSITION && t <= 3)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (HC.transitionState != HeroTransitionState.WAITING_TO_TRANSITION)
            {
                MiniDebugMod.Instance.Log("[WARNING] Failed to stop transitioning after 3 seconds, forcing savestate load");
            }
        }

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
            GM.nextSceneName = SecondaryRoom;
        }

        GM.cameraCtrl.SetMode(CameraController.CameraMode.FOLLOWING);

        Rigidbody2D krb2d = HC.gameObject.GetComponent<Rigidbody2D>();
        krb2d.gravityScale = KnightGravityScale;
        krb2d.velocity = KnightVelocity;

        var transformParent = KnightTransformParent == "" ? null : GameObject.Find(KnightTransformParent);
        if (transformParent != null)
        {
            HC.gameObject.transform.SetParent(transformParent.transform);
        }

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

                    var wait = fsm.Fsm.ActiveState.Actions.FirstOrDefault(a => a is Wait or WaitRandom);
                    if (wait != null)
                    {
                        var type = wait.GetType();
                        type.GetField("realTime").SetValue(wait, s.waitRealTime);
                        type.GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)!
                            .SetValue(wait, s.waitTimer);
                        type.GetField("startTime", BindingFlags.NonPublic | BindingFlags.Instance)!
                            .SetValue(wait, FsmTime.RealtimeSinceStartup - s.waitTimer);
                    }
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

        public ComponentActiveStatus(Component c, bool enabled)
        {
            this.go = c.gameObject.name;
            this.type = c.GetType().FullName;
            this.enabled = enabled;
        }
    }
}