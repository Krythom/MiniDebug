using System.Collections;
using GlobalEnums;
using MiniDebug.Util;
using MonoMod;
using UnityEngine;
using UnityEngine.SceneManagement;

#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

namespace MiniDebug.Patches
{
    [MonoModPatch("global::GameManager")]
    public class GameManager : global::GameManager
    {
#pragma warning disable CS0414 // Private field is assigned but its value is never used
        [MonoModIgnore] private bool timeSlowed;

        [MonoModIgnore] private bool tilemapDirty;
        [MonoModIgnore] private bool waitForManualLevelStart;
        [MonoModIgnore] public new event DestroyPooledObjects DestroyPersonalPools;
        [MonoModIgnore] public new event UnloadLevel UnloadingLevel;
        [MonoModIgnore] public new event LevelReady NextLevelReady;
        [MonoModIgnore] public new Scene nextScene { get; private set; }
#pragma warning restore CS0414 // Private field is assigned but its value is never used

        public extern IEnumerator orig_PauseGameToggle();

        public new IEnumerator PauseGameToggle()
        {
            // Check if the player is in a valid state to superslide
            if (!MiniDebug.Instance.Superslides || gameState != GameState.PLAYING
                || playerData.disablePause || !timeSlowed)
            {
                yield return orig_PauseGameToggle();
                yield break;
            }

            // Remove the freezeframe lock on pausing, reset recoil to ensure max speed
            timeSlowed = false;
            hero_ctrl.SetField("recoilSteps", 0);

            // Pause the game to cause a superslide
            yield return orig_PauseGameToggle();

            // Setting timeSlowed back to true causes the pause menu to be uncloseable
            // The freezeframe that slowed time is definitely over by now
            // orig_PauseGameToggle contains a large delay
        }

        [MonoModIgnore] private void ManualLevelStart() { }

        // Too lazy to IL mod this instead of just copy/pasting the decompiled code
        public new IEnumerator LoadSceneAdditive(string destScene)
        {
            tilemapDirty = true;
            startedOnThisScene = false;
            nextSceneName = destScene;
            waitForManualLevelStart = true;
            DestroyPersonalPools?.Invoke();
            UnloadingLevel?.Invoke();
            string exitingScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            nextScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(destScene);

            // This is the only new line
            yield return new WaitForSeconds(MiniDebug.Instance.LoadAdder);

            AsyncOperation loadop = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(destScene, LoadSceneMode.Additive);
            loadop.allowSceneActivation = true;
            yield return loadop;
            bool sceneUnloadOp = UnityEngine.SceneManagement.SceneManager.UnloadScene(exitingScene);
            RefreshTilemapInfo(destScene);
            ManualLevelStart();
            NextLevelReady?.Invoke();
        }
    }
}

#pragma warning restore CS0626 // Method, operator, or accessor is marked external and has no attributes on it
