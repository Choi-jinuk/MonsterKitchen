using UnityEngine;
using UnityEditor;

namespace MonsterKitchen.Editor
{
    public static class TestSceneSetup
    {
        [MenuItem("MonsterKitchen/Setup TestScene_PlayerMove")]
        public static void SetupPlayerMoveScene()
        {
            // Player
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("Player not found"); return; }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Char/Player.png");
            var ctrl   = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/PlayerAnimator.controller");
            if (sprite != null) player.GetComponent<SpriteRenderer>().sprite = sprite;
            if (ctrl   != null) player.GetComponent<Animator>().runtimeAnimatorController = ctrl;
            player.GetComponent<SpriteRenderer>().sortingOrder = 1;

            // Background
            var bg = GameObject.Find("Background");
            if (bg != null)
            {
                if (bg.GetComponent<SpriteRenderer>() == null)
                    bg.AddComponent<SpriteRenderer>();
                var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/BG/BG_Dungeon.png");
                var sr = bg.GetComponent<SpriteRenderer>();
                if (bgSprite != null) sr.sprite = bgSprite;
                sr.sortingOrder = -10;
                bg.transform.localScale = new Vector3(22f, 12f, 1f);
            }

            // Wall sizes
            SetWallScale("Wall_Top",    20f, 1f);
            SetWallScale("Wall_Bottom", 20f, 1f);
            SetWallScale("Wall_Left",   1f, 12f);
            SetWallScale("Wall_Right",  1f, 12f);

            // Global Light intensity
            var light = GameObject.Find("Global Light 2D");
            if (light != null)
            {
                var l2d = light.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
                if (l2d != null) l2d.intensity = 1f;
            }

            // Camera URP data
            var cam = Camera.main;
            if (cam != null)
            {
                var urpData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (urpData == null)
                    urpData = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }

            EditorUtility.SetDirty(player);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[TestSceneSetup] PlayerMove scene setup complete!");
        }

        static void SetWallScale(string name, float x, float y)
        {
            var go = GameObject.Find(name);
            if (go != null) go.transform.localScale = new Vector3(x, y, 1f);
        }
    }
}
