using System;
using System.Collections.Generic;
using GlobalEnums;
using UnityEngine;
using UnityEngine.SceneManagement;

using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MiniDebug.Hitbox
{
    public class HitboxManager : MonoBehaviour
    {
        private bool _showHitboxes;
        public bool ShowHitboxes
        {
            get => _showHitboxes;
            set
            {
                if (_showHitboxes == value)
                {
                    return;
                }

                _showHitboxes = value;

                if (_showHitboxes)
                {
                    SpawnHitboxes();
                }
                else
                {
                    RemoveHitboxes();
                }
            }
        }

        private HitboxRender hitboxRender;

        private void Awake()
        {
            USceneManager.activeSceneChanged -= SpawnHitboxes;
            USceneManager.activeSceneChanged += SpawnHitboxes;
        }

        private void RemoveHitboxes()
        {
            if (hitboxRender != null)
            {
                Destroy(hitboxRender);
                hitboxRender = null ;
            }
        }

        private void SpawnHitboxes(Scene _, Scene __) => SpawnHitboxes();

        private void SpawnHitboxes()
        {
            RemoveHitboxes();
            if (!GameManager.instance.IsGameplayScene() || !ShowHitboxes)
            {
                return;
            }

            hitboxRender = gameObject.AddComponent<HitboxRender>();
        }
    }
}
