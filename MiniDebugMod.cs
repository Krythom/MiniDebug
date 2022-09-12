using System.Collections;
using GlobalEnums;
using Modding;

namespace MiniDebug
{
    public class MiniDebugMod : Mod
    {
        public static MiniDebugMod Instance { get; private set; }

        public override string Version => "0.1.3";

        public MiniDebugMod() : base("Mini Debug")
        {
            Instance = this;
            _ = MiniDebug.Instance;

            On.SceneManager.DrawBlackBorders += KillBlackBorders;
            On.GameManager.PauseGameToggle += PatchSuperslides;
        }

        public override float BeforeAdditiveLoad(string scene)
            => MiniDebug.Instance.LoadAdder;

        private IEnumerator PatchSuperslides(On.GameManager.orig_PauseGameToggle orig, GameManager self)
        {
            // Check if the player is in a valid state to superslide
            if (!MiniDebug.Instance.Superslides
                || PlayerData.instance.disablePause
                || self.gameState != GameState.PLAYING
                || !self.GetField<GameManager, bool>("timeSlowed"))
            {
                yield return orig(self);
                yield break;
            }

            // Remove the freezeframe lock on pausing, reset recoil to ensure max speed
            self.SetField("timeSlowed", false);
            HeroController.instance.SetField("recoilSteps", 0);

            // Pause the game to cause a superslide
            yield return orig(self);

            // Setting timeSlowed back to true causes the pause menu to be uncloseable
            // The freezeframe that slowed time is definitely over by now
            // orig_PauseGameToggle contains a large delay
        }

        private void KillBlackBorders(On.SceneManager.orig_DrawBlackBorders orig, SceneManager self)
        {
        }
    }
}
