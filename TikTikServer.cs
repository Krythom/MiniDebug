using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using WebSocketSharp.Server;

using UObject = UnityEngine.Object;

namespace MiniDebug
{
    // Pretty much only useful for low jump segmented runs, but allie wanted it and she is cool
    public class TikTikServer : WebSocketBehavior
    {
        private readonly List<GameObject> TikTiks = new List<GameObject>();

        public void ResetTikTiks(Scene from, Scene to)
        {
            TikTiks.Clear();
            TikTiks.AddRange(UObject.FindObjectsOfType<GameObject>()
                .Where(obj => obj.name.StartsWith("Climber ")));
        }

        public void SendTikTiks()
        {
            List<string> jsonPos = new List<string>();
            foreach (GameObject tiktik in TikTiks)
            {
                if (tiktik == null)
                {
                    continue;
                }

                jsonPos.Add($"{{\"x\":{tiktik.transform.position.x},\"y\":{tiktik.transform.position.y}}}");
            }

            if (jsonPos.Count > 0)
            {
                Send("[" + string.Join(", ", jsonPos.ToArray()) + "]");
            }
        }
    }
}
