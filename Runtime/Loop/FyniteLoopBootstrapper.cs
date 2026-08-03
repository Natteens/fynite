using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Fynite
{
    internal static class FyniteLoopBootstrapper
    {
        internal struct FyniteUpdate
        {
        }

        internal struct FyniteFixedUpdate
        {
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Bootstrap()
        {
            FyniteLoop.Clear();
            Install();
        }

        internal static void Install()
        {
            var root = PlayerLoop.GetCurrentPlayerLoop();

            FynitePlayerLoopUtility.Remove(ref root, typeof(FyniteUpdate));
            FynitePlayerLoopUtility.Remove(ref root, typeof(FyniteFixedUpdate));

            var update = new PlayerLoopSystem
            {
                type = typeof(FyniteUpdate),
                updateDelegate = FyniteLoop.RunUpdate
            };

            var fixedUpdate = new PlayerLoopSystem
            {
                type = typeof(FyniteFixedUpdate),
                updateDelegate = FyniteLoop.RunFixedUpdate
            };

            if (!FynitePlayerLoopUtility.InsertAfter(ref root, typeof(Update.ScriptRunBehaviourUpdate), update))
            {
                FynitePlayerLoopUtility.AppendTo(ref root, typeof(Update), update);
            }

            if (!FynitePlayerLoopUtility.InsertAfter(
                    ref root,
                    typeof(FixedUpdate.ScriptRunBehaviourFixedUpdate),
                    fixedUpdate))
            {
                FynitePlayerLoopUtility.AppendTo(ref root, typeof(FixedUpdate), fixedUpdate);
            }

            PlayerLoop.SetPlayerLoop(root);
        }

        internal static void Uninstall()
        {
            var root = PlayerLoop.GetCurrentPlayerLoop();
            FynitePlayerLoopUtility.Remove(ref root, typeof(FyniteUpdate));
            FynitePlayerLoopUtility.Remove(ref root, typeof(FyniteFixedUpdate));
            PlayerLoop.SetPlayerLoop(root);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookEditor()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            FyniteLoop.Clear();
            Uninstall();
        }
#endif
    }
}
