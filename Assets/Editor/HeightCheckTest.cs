using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Unity.AI.Assistant.PlayModeTest
{
    [InitializeOnLoad]
    internal static class PlayModeTestRunner
    {
        private const string StateKey = "PlayModeTest.State";
        private const string ResultKey = "PlayModeTest.Result";
        private const string ScriptPathKey = "PlayModeTest.ScriptPath";
        private const string SentinelLog = "PLAY_MODE_TEST_COMPLETE";

        static PlayModeTestRunner()
        {
            string state = SessionState.GetString(StateKey, "Idle");
            if (state == "WaitingForCompile")
            {
                EditorApplication.delayCall += () =>
                {
                    SessionState.SetString(StateKey, "EnteringPlayMode");
                    EditorApplication.isPlaying = true;
                };
            }
            else if (state == "InPlayMode" && EditorApplication.isPlaying)
            {
                EditorApplication.update += WaitAndRun;
            }
            else if (state == "Done")
            {
                Debug.Log(SentinelLog);
                EditorApplication.delayCall += SelfDestruct;
            }
        }

        private static int _frames = 0;
        private static void WaitAndRun()
        {
            _frames++;
            if (_frames < 30) return; // Wait 30 frames for spawning/physics
            EditorApplication.update -= WaitAndRun;

            GameObject player = GameObject.Find("playerPrefab(Clone)") ?? GameObject.FindGameObjectWithTag("Player");
            
            var result = new TestResult { success = true };
            if (player != null)
            {
                result.playerY = player.transform.position.y;
                result.hipsY = FindHips(player.transform)?.position.y ?? -99f;
                result.colliderBottomY = player.GetComponent<CapsuleCollider>()?.bounds.min.y ?? -99f;
                
                var anim = player.GetComponent<Animator>();
                result.isRootMotion = anim?.applyRootMotion ?? false;
                result.animState = anim?.GetCurrentAnimatorStateInfo(0).fullPathHash.ToString() ?? "none";
            }
            else
            {
                result.success = false;
                result.error = "Player not found";
            }

            SessionState.SetString(ResultKey, JsonUtility.ToJson(result));
            SessionState.SetString(StateKey, "Done");
            EditorApplication.isPlaying = false;
        }

        private static Transform FindHips(Transform t)
        {
            if (t.name.Contains("Hips")) return t;
            foreach (Transform child in t)
            {
                var found = FindHips(child);
                if (found != null) return found;
            }
            return null;
        }

        private static void SelfDestruct()
        {
            string scriptPath = SessionState.GetString(ScriptPathKey, "");
            if (!string.IsNullOrEmpty(scriptPath) && AssetDatabase.AssetPathExists(scriptPath))
            {
                AssetDatabase.DeleteAsset(scriptPath);
            }
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ScriptPathKey);
        }

        [System.Serializable]
        private class TestResult
        {
            public bool success;
            public string error;
            public float playerY;
            public float hipsY;
            public float colliderBottomY;
            public bool isRootMotion;
            public string animState;
        }
    }
}
