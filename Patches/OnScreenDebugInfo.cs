using MonoMod;

#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it

namespace MiniDebug.Patches
{
    [MonoModPatch("global::OnScreenDebugInfo")]
    public class OnScreenDebugInfo : global::OnScreenDebugInfo
    {
        private extern void orig_Awake();

        private void Awake()
        {
            orig_Awake();

            // this call to Instance causes it to create one
            _ = MiniDebug.Instance;
        }
    }
}

#pragma warning restore CS0626 // Method, operator, or accessor is marked external and has no attributes on it
