using UnityEngine;

namespace MiniDebug.Util;

public static class GUIHelper
{
    public static Config SaveConfig()
    {
        Config cfg = new Config
        {
            backgroundColor = GUI.backgroundColor,
            contentColor = GUI.contentColor,
            color = GUI.color,
            matrix = GUI.matrix
        };

        GUI.backgroundColor = Color.white;
        GUI.contentColor = Color.white;
        GUI.color = Color.white;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1f));

        return cfg;
    }

    public static void RestoreConfig(Config cfg)
    {
        GUI.backgroundColor = cfg.backgroundColor;
        GUI.contentColor = cfg.contentColor;
        GUI.color = cfg.color;
        GUI.matrix = cfg.matrix;
    }

    public struct Config
    {
        public Color backgroundColor;
        public Color contentColor;
        public Color color;
        public Matrix4x4 matrix;
    }
}