using System;
using System.Collections.Generic;
using GlobalEnums;
using UnityEngine;
using UnityEngine.SceneManagement;

using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MiniDebug
{
    public class HitboxManager : MonoBehaviour
    {
        private bool _showHitboxes;
        public bool ShowHitboxes
        {
            get => _showHitboxes;
            set
            {
                if (_showHitboxes && !value)
                {
                    RemoveHitboxes();
                }
                else if (!_showHitboxes && value)
                {
                    SpawnHitboxes(default, default);
                }

                _showHitboxes = value;
            }
        }

        private Material greenMat;
        private Material redMat;
        private Material yellowMat;
        private Material blueMat;

        private Dictionary<Collider2D, LineRenderer> lines = new Dictionary<Collider2D, LineRenderer>();
        private List<Collider2D> colliders = new List<Collider2D>();

        private void Awake()
        {
            greenMat = new Material(Shader.Find("Diffuse"))
            {
                renderQueue = 4000,
                color = Color.green
            };

            redMat = new Material(Shader.Find("Diffuse"))
            {
                renderQueue = 4000,
                color = Color.red
            };

            yellowMat = new Material(Shader.Find("Diffuse"))
            {
                renderQueue = 4000,
                color = Color.yellow
            };

            blueMat = new Material(Shader.Find("Diffuse"))
            {
                renderQueue = 4000,
                color = Color.blue
            };

            USceneManager.activeSceneChanged -= SpawnHitboxes;
            USceneManager.activeSceneChanged += SpawnHitboxes;
        }

        private void Update()
        {
            foreach (Collider2D collider2D in colliders)
            {
                if (collider2D == null || !collider2D.enabled)
                {
                    if (lines[collider2D] != null)
                    {
                        Destroy(lines[collider2D].gameObject);
                    }
                }
                else
                {
                    lines[collider2D] = SetupLineRenderer(collider2D, lines[collider2D], null);
                }
            }
        }

        private void RemoveHitboxes()
        {
            foreach (GameObject gameObject in FindObjectsOfType<GameObject>())
            {
                if (gameObject.name == "Mod Hitbox")
                {
                    Destroy(gameObject);
                }
                colliders.Clear();
            }
        }

        private void SpawnHitboxes(Scene from, Scene to)
        {
            if (!GameManager.instance.IsGameplayScene() || !ShowHitboxes)
            {
                return;
            }

            foreach (LineRenderer line in lines.Values)
            {
                if (line != null)
                {
                    Destroy(line.gameObject);
                }
            }

            colliders = new List<Collider2D>();
            lines = new Dictionary<Collider2D, LineRenderer>();

            foreach (Collider2D col in FindObjectsOfType<Collider2D>())
            {
                if (colliders.Contains(col))
                {
                    continue;
                }

                if (col.gameObject.layer == (int)PhysLayers.TERRAIN)
                {
                    lines.Add(col, SetupLineRenderer(col, null, greenMat));
                }
                else if (col.GetComponent<TransitionPoint>())
                {
                    lines.Add(col, SetupLineRenderer(col, null, blueMat));
                }
                else if (FSMUtility.LocateFSM(col.gameObject, "damages_hero"))
                {
                    colliders.Add(col);
                    lines.Add(col, SetupLineRenderer(col, null, redMat));
                }
                else if (col.gameObject == HeroController.instance.gameObject && !col.isTrigger)
                {
                    colliders.Add(col);
                    lines.Add(col, SetupLineRenderer(col, null, yellowMat));
                }
                else if (col.isTrigger && col.gameObject.GetComponent<HazardRespawnTrigger>() != null)
                {
                    colliders.Add(col);
                    lines.Add(col, SetupLineRenderer(col, null, blueMat));
                }
                else if (col.isTrigger && col.gameObject.GetComponent<CircleCollider2D>() == null)
                {
                    colliders.Add(col);
                    lines.Add(col, SetupLineRenderer(col, null, yellowMat));
                }
            }
        }

        private LineRenderer SetupLineRenderer(Collider2D col, LineRenderer line = null, Material mat = null)
        {
            if (line == null)
            {
                if (mat == null)
                {
                    mat = greenMat;
                }

                GameObject lineObj = new GameObject("Mod Hitbox");
                lineObj.transform.SetParent(col.transform);
                lineObj.transform.position = Vector3.zero;

                line = lineObj.AddComponent<LineRenderer>();
                line.SetWidth(0.07f, 0.07f);
                line.sharedMaterial = mat;
            }

            if (col is BoxCollider2D box)
            {
                Vector2 topRight = box.size / 2f;
                Vector2 botLeft = -topRight;
                Vector2 botRight = new Vector2(topRight.x, botLeft.y);
                Vector2 topLeft = -botRight;

                line.SetVertexCount(5);
                line.SetPositions(new Vector3[]
                {
                    col.transform.TransformPoint(botLeft + box.offset),
                    col.transform.TransformPoint(topLeft + box.offset),
                    col.transform.TransformPoint(topRight + box.offset),
                    col.transform.TransformPoint(botRight + box.offset),
                    col.transform.TransformPoint(botLeft + box.offset)
                });
            }
            else if (col is CircleCollider2D circle)
            {
                Vector3[] points = new Vector3[30];
                float sliceSize = 2f * (float)Math.PI / points.Length;

                for (int i = 0; i < points.Length - 1; i++)
                {
                    float theta = sliceSize * i;
                    float sin = (float)Math.Sin(theta);
                    float cos = (float)Math.Cos(theta);
                    points[i] = new Vector2(
                        (cos - sin) * circle.radius + col.transform.position.x,
                        (cos + sin) * circle.radius + col.transform.position.y);
                }

                points[points.Length - 1] = points[0];

                line.SetVertexCount(points.Length);
                line.SetPositions(points);
            }
            else if (col is PolygonCollider2D poly)
            {
                Vector3[] points = new Vector3[poly.points.Length + 1];
                for (int j = 0; j < poly.points.Length; j++)
                {
                    points[j] = poly.transform.TransformPoint(poly.points[j]);
                }

                points[points.Length - 1] = points[0];
                line.SetVertexCount(points.Length);
                line.SetPositions(points);
            }
            else if (col is EdgeCollider2D edge)
            {
                Vector3[] points = new Vector3[edge.points.Length];
                for (int k = 0; k < edge.points.Length; k++)
                {
                    points[k] = edge.transform.TransformPoint(edge.points[k]);
                }

                line.SetVertexCount(points.Length);
                line.SetPositions(points);
            }

            return line;
        }
    }
}
