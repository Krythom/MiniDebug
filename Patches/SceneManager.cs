using MonoMod;

namespace MiniDebug.Patches
{
    [MonoModPatch("global::SceneManager")]
    public class SceneManager : global::SceneManager
    {
        // Overwrite the original to prevent scene borders
        private void DrawBlackBorders() { }
    }
}
