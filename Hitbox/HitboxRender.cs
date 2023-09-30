﻿using GlobalEnums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MiniDebug.Hitbox
{
    public class HitboxRender : MonoBehaviour
    {
        public static Matrix4x4 camWorldToScreenMatrix;
        public static Matrix4x4 camProjectionMatrix;

        // ReSharper disable once StructCanBeMadeReadOnly
        private struct HitboxType : IComparable<HitboxType>
        {
            public static readonly HitboxType Knight = new(Color.yellow, 0);                     // yellow
            public static readonly HitboxType Enemy = new(new Color(0.8f, 0, 0), 1);       // red      
            public static readonly HitboxType Attack = new(Color.cyan, 2);                       // cyan
            public static readonly HitboxType Terrain = new(new Color(0, 0.8f, 0), 3);     // green
            public static readonly HitboxType Trigger = new(new Color(0.5f, 0.5f, 1f), 4); // blue
            public static readonly HitboxType Breakable = new(new Color(1f, 0.75f, 0.8f), 5); // pink
            public static readonly HitboxType Gate = new(new Color(0.0f, 0.0f, 0.5f), 6); // dark blue
            public static readonly HitboxType HazardRespawn = new(new Color(0.5f, 0.0f, 0.5f), 7); // purple 
            public static readonly HitboxType Other = new(new Color(0.9f, 0.6f, 0.4f), 8); // orange


            public readonly Color Color;
            public readonly int Depth;

            private HitboxType(Color color, int depth)
            {
                Color = color;
                Depth = depth;
            }

            public int CompareTo(HitboxType other)
            {
                return other.Depth.CompareTo(Depth);
            }
        }

        private readonly SortedDictionary<HitboxType, HashSet<Collider2D>> colliders = new()
        {
            {HitboxType.Knight, new HashSet<Collider2D>()},
            {HitboxType.Enemy, new HashSet<Collider2D>()},
            {HitboxType.Attack, new HashSet<Collider2D>()},
            {HitboxType.Terrain, new HashSet<Collider2D>()},
            {HitboxType.Trigger, new HashSet<Collider2D>()},
            {HitboxType.Breakable, new HashSet<Collider2D>()},
            {HitboxType.Gate, new HashSet<Collider2D>()},
            {HitboxType.HazardRespawn, new HashSet<Collider2D>()},
            {HitboxType.Other, new HashSet<Collider2D>()},
        };

        public static float LineWidth => Math.Max(0.7f, Screen.width / 960f * GameCameras.instance.tk2dCam.ZoomFactor);

        private void Start()
        {
            foreach (Collider2D col in Resources.FindObjectsOfTypeAll<Collider2D>())
            {
                TryAddHitboxes(col);
            }
        }

        public void UpdateHitbox(GameObject go)
        {
            foreach (Collider2D col in go.GetComponentsInChildren<Collider2D>(true))
            {
                TryAddHitboxes(col);
            }
        }

        private Vector2 LocalToScreenPoint(Camera camera, Collider2D collider2D, Vector2 point)
        {
            Vector2 result = MiniDebug.Instance.CameraFollow ? manualWolrdToScreenPoint((Vector2)collider2D.transform.TransformPoint(point + collider2D.offset)) 
                : camera.WorldToScreenPoint((Vector2)collider2D.transform.TransformPoint(point + collider2D.offset));
            return new Vector2((int)Math.Round(result.x), (int)Math.Round(Screen.height - result.y));
        }

        private void TryAddHitboxes(Collider2D collider2D)
        {
            if (collider2D == null)
            {
                return;
            }

            if (collider2D is BoxCollider2D or PolygonCollider2D or EdgeCollider2D or CircleCollider2D)
            {
                GameObject go = collider2D.gameObject;
                if (collider2D.gameObject.LocateMyFSM("damages_hero"))
                {
                    colliders[HitboxType.Enemy].Add(collider2D);
                }
                else if (go.LocateMyFSM("health_manager_enemy") || go.LocateMyFSM("health_manager"))
                {
                    colliders[HitboxType.Other].Add(collider2D);
                }
                else if (go.layer == (int)PhysLayers.TERRAIN)
                {
                    if (go.name.Contains("Breakable") || go.name.Contains("Collapse")) colliders[HitboxType.Breakable].Add(collider2D);
                    else colliders[HitboxType.Terrain].Add(collider2D);
                }
                else if (go == HeroController.instance?.gameObject && !collider2D.isTrigger)
                {
                    colliders[HitboxType.Knight].Add(collider2D);
                }
                else if (go.LocateMyFSM("damages_enemy") || go.name == "Damager" && go.LocateMyFSM("Damage"))
                {
                    colliders[HitboxType.Attack].Add(collider2D);
                }
                else if (collider2D.isTrigger && collider2D.GetComponent<HazardRespawnTrigger>())
                {
                    colliders[HitboxType.HazardRespawn].Add(collider2D);
                }
                else if (collider2D.isTrigger && collider2D.GetComponent<TransitionPoint>())
                {
                    colliders[HitboxType.Gate].Add(collider2D);
                }
                else if (collider2D.GetComponent<NonBouncer>())
                {
                    colliders[HitboxType.Trigger].Add(collider2D);
                }
                else
                {
                    colliders[HitboxType.Other].Add(collider2D);
                }
            }
        }

        private void OnGUI()
        {
            if (Event.current?.type != EventType.Repaint || Camera.main == null || GameManager.instance == null || GameManager.instance.isPaused)
            {
                return;
            }

            GUI.depth = int.MaxValue;
            Camera camera = Camera.main;
            float lineWidth = LineWidth;
            foreach (var pair in colliders)
            {
                foreach (Collider2D collider2D in pair.Value)
                {
                    DrawHitbox(camera, collider2D, pair.Key, lineWidth);
                }
            }
        }

        private void DrawHitbox(Camera camera, Collider2D collider2D, HitboxType hitboxType, float lineWidth)
        {
            if (collider2D == null || !collider2D.isActiveAndEnabled)
            {
                return;
            }

            int origDepth = GUI.depth;
            GUI.depth = hitboxType.Depth;
            if (collider2D is BoxCollider2D or EdgeCollider2D or PolygonCollider2D)
            {
                switch (collider2D)
                {
                    case BoxCollider2D boxCollider2D:
                        Vector2 halfSize = boxCollider2D.size / 2f;
                        Vector2 topLeft = new(-halfSize.x, halfSize.y);
                        Vector2 topRight = halfSize;
                        Vector2 bottomRight = new(halfSize.x, -halfSize.y);
                        Vector2 bottomLeft = -halfSize;
                        List<Vector2> boxPoints = new List<Vector2>
                        {
                            topLeft, topRight, bottomRight, bottomLeft, topLeft
                        };
                        DrawPointSequence(boxPoints, camera, collider2D, hitboxType, lineWidth);
                        break;
                    case EdgeCollider2D edgeCollider2D:
                        DrawPointSequence(new(edgeCollider2D.points), camera, collider2D, hitboxType, lineWidth);
                        break;
                    case PolygonCollider2D polygonCollider2D:
                        for (int i = 0; i < polygonCollider2D.pathCount; i++)
                        {
                            List<Vector2> polygonPoints = new(polygonCollider2D.GetPath(i));
                            if (polygonPoints.Count > 0)
                            {
                                polygonPoints.Add(polygonPoints[0]);
                            }
                            DrawPointSequence(polygonPoints, camera, collider2D, hitboxType, lineWidth);
                        }
                        break;
                }
            }
            else if (collider2D is CircleCollider2D circleCollider2D)
            {
                Vector2 center = LocalToScreenPoint(camera, collider2D, Vector2.zero);
                Vector2 right = LocalToScreenPoint(camera, collider2D, Vector2.right * circleCollider2D.radius);
                int radius = (int)Math.Round(Vector2.Distance(center, right));
                Drawing.DrawCircle(center, radius, hitboxType.Color, lineWidth, true, Mathf.Clamp(radius / 16, 4, 32));
            }

            GUI.depth = origDepth;
        }

        private void DrawPointSequence(List<Vector2> points, Camera camera, Collider2D collider2D, HitboxType hitboxType, float lineWidth)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 pointA = LocalToScreenPoint(camera, collider2D, points[i]);
                Vector2 pointB = LocalToScreenPoint(camera, collider2D, points[i + 1]);
                Drawing.DrawLine(pointA, pointB, hitboxType.Color, lineWidth, true);
            }
        }

        private Vector3 manualWolrdToScreenPoint(Vector3 wp)
        {
            // calculate view-projection matrix
            Matrix4x4 mat = camProjectionMatrix * camWorldToScreenMatrix;

            // multiply world point by VP matrix
            Vector4 temp = mat * new Vector4(wp.x, wp.y, wp.z, 1f);

            if (temp.w == 0f)
            {
                // point is exactly on camera focus point, screen point is undefined
                // unity handles this by returning 0,0,0
                return Vector3.zero;
            }
            else
            {
                // convert x and y from clip space to window coordinates
                temp.x = (temp.x / temp.w + 1f) * .5f * Camera.main.pixelWidth;
                temp.y = (temp.y / temp.w + 1f) * .5f * Camera.main.pixelHeight;
                return new Vector3(temp.x, temp.y, wp.z);
            }
        }
    }
}
