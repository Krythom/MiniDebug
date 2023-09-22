using System;

namespace MiniDebug
{
    [Serializable]
    public class Settings
    {
        public string showSpeed = "f1";
        public string infiniteHealth = "f2";
        public string infiniteSoul = "f3";
        public string increaseLoadTime = "f5";
        public string decreaseLoadTime = "f6";
        public string toggleSuperslides = "f7";
        public string cameraFollow = "f8";
        public string transparentInv = "f9";
        public string reloadSettings = "f12";
        public string noclip = "[0]";
        public string yeetLoadScreens = "[4]";
        public string showHitboxes = "[5]";
        public string createSaveState = "[7]";
        public string createDetailedSaveState = "[6]";
        public string loadSaveState = "[8]";
        public string loadSaveStateDuped = "[9]";
        public string kill = "[*]";
        public string dupeRoom = "[+]";
        public string zoomIn = "page up";
        public string zoomOut = "page down";
        public string resetZoom = "home";
        public string hideVignette = "insert";
        public string increaseTimeScale = "[";
        public string decreaseTimeScale = "]";
        public string resetTimeScale = "\\";
        public string giveBadFloat = "f";
        public string revealHiddenAreas = "r";
        public string increaseSelectedLoad = "=";
        public string decreaseSelectedLoad = "-";
        public string toggleLoads = "t";
        public string camSetup = "b";

        // public string _DEBUG = "y";
    }
}
