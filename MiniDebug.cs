using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlobalEnums;
using MiniDebug.Util;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;

using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MiniDebug
{
    public class MiniDebug : MonoBehaviour
    {
        private static MiniDebug _instance;
        public static MiniDebug Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MiniDebug>();
                }

                if (_instance == null)
                {
                    GameObject gameObject = new GameObject();
                    _instance = gameObject.AddComponent<MiniDebug>();
                    _instance.SaveStateManager = gameObject.AddComponent<SaveStateManager>();
                    _instance.HitboxManager = gameObject.AddComponent<HitboxManager>();

                    DontDestroyOnLoad(gameObject);
                }

                return _instance;
            }
        }

        // Vanilla game object references
        private static HeroController HC => HeroController.instance;
        private static GameManager GM => GameManager.instance;

        private static PlayerData PD
        {
            get => PlayerData.instance;
            set => PlayerData.instance = value;
        }

        private static Rigidbody2D _hcRb2d;
        private static Rigidbody2D HCRb2d
            => _hcRb2d == null
                ? _hcRb2d = HC.GetField<HeroController, Rigidbody2D>("rb2d")
                : _hcRb2d;

        // MiniDebug sub-components
        public Settings Settings { get; private set; } = new Settings();
        public SaveStateManager SaveStateManager { get; private set; }
        public HitboxManager HitboxManager { get; private set; }

        // Active cheat values
        private bool _noclip;
        public bool NoClip
        {
            get => _noclip;
            private set
            {
                _noclip = value;
                if (_noclip)
                {
                    _noclipPos = HC.transform.position;
                }
            }
        }

        private bool _cameraFollow;
        public bool CameraFollow
        {
            get => _cameraFollow;
            private set
            {
                _cameraFollow = value;
                if (!CameraFollow)
                {
                    GM.cameraCtrl.SetField("isGameplayScene", GM.IsGameplayScene());
                }
            }
        }

        private float _timescale = 1f;
        public float TimeScale
        {
            get => _timescale;
            private set
            {
                _timescale = value;
                if (_timescale == 1f && Time.timeScale != 0f)
                {
                    Time.timeScale = 1f;
                }
            }
        }

        public bool Superslides { get; private set; } = true;
        public bool ShowSpeed { get; private set; }
        public bool VignetteDisabled { get; private set; }
        public bool InfSoul { get; private set; }
        public bool InfHealth { get; private set; }
        public int LoadAdder { get; private set; } = 1;

        // Cheat method dictionary, filled in ReloadSettings
        private Dictionary<string, Action> _binds;

        // Misc fields
        private readonly List<Renderer> _invRenders = new List<Renderer>();
        private Vector3 _noclipPos;
        public bool AcceptingInput { get; set; } = true;

        public delegate void UpdateEvent();
        public event UpdateEvent OnUpdate;

        private void Start()
        {
            ReloadSettings();
        }

        private void Update()
        {
            OnUpdate?.Invoke();

            if (AcceptingInput)
            {
                foreach ((string key, Action action) in _binds)
                {
                    if (Input.GetKeyDown(key))
                    {
                        action();
                    }
                }
            }

            HC.vignette.enabled = !VignetteDisabled;

            if (InfHealth)
            {
                HC.MaxHealth();
            }

            if (InfSoul)
            {
                HC.AddMPCharge(999);
            }

            if (Time.timeScale != TimeScale && Time.timeScale != 0f && TimeScale != 1f)
            {
                Time.timeScale = TimeScale;
            }

            if (CameraFollow)
            {
                GM.cameraCtrl.SetField("isGameplayScene", false);
                GM.cameraCtrl.camTarget.transform.position = new Vector3(HC.gameObject.transform.position.x, HC.gameObject.transform.position.y, GM.cameraCtrl.camTarget.transform.position.z);
                GM.cameraCtrl.transform.position = new Vector3(HC.gameObject.transform.position.x, HC.gameObject.transform.position.y, GM.cameraCtrl.transform.position.z);
            }

            if (!NoClip)
            {
                return;
            }

            if (GM.inputHandler.inputActions.left.IsPressed)
            {
                _noclipPos += Vector3.left * Time.deltaTime * 20f;
            }

            if (GM.inputHandler.inputActions.right.IsPressed)
            {
                _noclipPos += Vector3.right * Time.deltaTime * 20f;
            }

            if (GM.inputHandler.inputActions.up.IsPressed)
            {
                _noclipPos += Vector3.up * Time.deltaTime * 20f;
            }

            if (GM.inputHandler.inputActions.down.IsPressed)
            {
                _noclipPos += Vector3.down * Time.deltaTime * 20f;
            }

            // Checking for sly storeroom is a dirty fix for a savestate bug
            // This scene is unimportant enough that this shouldn't cause issues
            if (HC.transitionState == HeroTransitionState.WAITING_TO_TRANSITION
                && GM.GetSceneNameString() != "Room_Sly_Storeroom")
            {
                HC.gameObject.transform.position = _noclipPos;
            }
            else
            {
                _noclipPos = HC.gameObject.transform.position;
            }
        }

        private void OnGUI()
        {
            if (GM.GetSceneNameString() == Constants.MENU_SCENE || !ShowSpeed)
            {
                return;
            }

            GUIHelper.Config cfg = GUIHelper.SaveConfig();

            GUI.Label(new Rect(0f, 0f, 200f, 200f), $"(X, Y): {HCRb2d.velocity.x}, {HCRb2d.velocity.y}");
            GUI.Label(new Rect(0f, 50f, 200f, 200f), $"(Xpos, Ypos) {HCRb2d.position.x}, {HCRb2d.position.y}");
            GUI.Label(new Rect(0f, 100f, 200f, 200f), $"(Scene Name) {GM.sceneName}");
            GUI.Label(new Rect(0f, 150f, 200f, 200f), $"(Bench Room) {PD.respawnScene}");
            GUI.Label(new Rect(0f, 200f, 200f, 200f), $"(Load Extension) {LoadAdder}");
            GUI.Label(new Rect(0f, 250f, 200f, 200f), $"(Timescale) {TimeScale}");
            GUI.Label(new Rect(0f, 300f, 200f, 200f), $"(soul) {PD.MPCharge}");

            GUIHelper.RestoreConfig(cfg);
        }

        private void ReloadSettings()
        {
            string path = Application.persistentDataPath + "/minidebug.json";
            if (File.Exists(path))
            {
                Settings = JsonUtility.FromJson<Settings>(File.ReadAllText(path));
            }

            File.WriteAllText(path, JsonUtility.ToJson(Settings, true));
            SaveStateManager.LoadStateNames();

            _binds = new Dictionary<string, Action>
            {
                [Settings.showSpeed] = () => ShowSpeed = !ShowSpeed,
                [Settings.infiniteHealth] = () => InfHealth = !InfHealth,
                [Settings.infiniteSoul] = () => InfSoul = !InfSoul,
                [Settings.increaseLoadTime] = () => LoadAdder++,
                [Settings.decreaseLoadTime] = () => LoadAdder--,
                [Settings.toggleSuperslides] = () => Superslides = !Superslides,
                [Settings.cameraFollow] = () => CameraFollow = !CameraFollow,
                [Settings.transparentInv] = ToggleInventory,
                [Settings.reloadSettings] = ReloadSettings,
                [Settings.noclip] = () => NoClip = !NoClip,
                [Settings.yeetLoadScreens] = DestroyLoadScreens,
                [Settings.showHitboxes] = () => HitboxManager.ShowHitboxes = !HitboxManager.ShowHitboxes,
                [Settings.createSaveState] = SaveStateManager.SaveState,
                [Settings.loadSaveState] = () => SaveStateManager.LoadSaveState(false),
                [Settings.loadSaveStateDuped] = () => SaveStateManager.LoadSaveState(true),
                [Settings.kill] = () => HC.StartCoroutine("Die"),
                [Settings.dupeRoom] = () => USceneManager.LoadScene(GM.GetSceneNameString(), LoadSceneMode.Additive),
                [Settings.zoomIn] = () => GameCameras.instance.tk2dCam.ZoomFactor *= 1.05f,
                [Settings.zoomOut] = () => GameCameras.instance.tk2dCam.ZoomFactor /= 1.05f,
                [Settings.resetZoom] = () => GameCameras.instance.tk2dCam.ZoomFactor = 1f,
                [Settings.hideVignette] = () => VignetteDisabled = !VignetteDisabled,
                [Settings.increaseTimeScale] = () => TimeScale += 0.1f,
                [Settings.decreaseTimeScale] = () => TimeScale -= 0.1f,
                [Settings.resetTimeScale] = () => TimeScale = 1f
            };
        }

        private void ToggleInventory()
        {
            _invRenders.RemoveAll(r => r == null);
            if (_invRenders.Count == 0)
            {
                foreach (Renderer renderer in GameObject.FindGameObjectWithTag("Inventory Top").GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.enabled)
                    {
                        _invRenders.Add(renderer);
                    }
                }
            }

            foreach (Renderer renderer in _invRenders)
            {
                renderer.enabled = !renderer.enabled;
            }
        }

        private void DestroyLoadScreens()
        {
            foreach (GameObject obj in FindObjectsOfType<GameObject>()
                .Where(obj => obj.name.Contains("Blanker")))
            {
                Destroy(obj);
            }
        }
    }
}
