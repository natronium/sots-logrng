using Type = System.Type;
using Environment = System.Environment;
using BepInEx;
using HarmonyLib;

using System.Runtime.InteropServices;
using Random = UnityEngine.Random;

namespace LogRngCalls
{
    [StructLayout(LayoutKind.Explicit)]
    public struct RandomStateWrapper
    {
        [FieldOffset(0)] public Random.State state;
        [FieldOffset(0)] public uint v0;
        [FieldOffset(4)] public uint v1;
        [FieldOffset(8)] public uint v2;
        [FieldOffset(12)] public uint v3;
        [FieldOffset(0)] public int s0;
        [FieldOffset(4)] public int s1;
        [FieldOffset(8)] public int s2;
        [FieldOffset(12)] public int s3;
        public static implicit operator RandomStateWrapper(Random.State aState)
        {
            return new RandomStateWrapper { state = aState };
        }
        public static implicit operator Random.State(RandomStateWrapper aState)
        {
            return aState.state;
        }

        public override string ToString()
        {
            return string.Format("{0:X8} {1:X8} {2:X8} {3:X8}", v0, v1, v2, v3);
        }
    }    

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        /*
        We want to hook:
        * System.Random (used by ink)
        ** ctor
        ** Next()

        * UnityEngine.Random
        ** InitState
        ** state {get; set;}
        ** value {get;}
        ** Range

        We *might* want to hook: 
        * EchoDog.SaveManager
        ** ImportantRandomStart
        ** ImportantRandomEnd
        ** ImportantRandomValue
        ** ImportantRandomElement

        * EchoDog.Utilities
        ** PushRandomState
        ** PopRandomState
        */

        static Plugin instance;

        static bool isCurrentlyImportantTime = false;

        private void Awake()
        {
            instance = this;
            var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            Logger.LogInfo("ohai");

        }

        void LogRngCall()
        {
            if(isCurrentlyImportantTime) {
                // Logger.LogInfo(Environment.StackTrace);
                RandomStateWrapper state = Random.state;
                Logger.LogInfo(state);
            }
        }

        void LogInfo(string message) {
            if(isCurrentlyImportantTime) {
                Logger.LogInfo(message);
            }
        }


        // there is probably a cleaner way to do this than to hit it with a ton of boilerplate
        // especially since it's all doing the same thing but this seems the most straightforward
        // this is certainly better than giving each patch its own class though
        [HarmonyPatch(typeof(UnityEngine.Random))]
        private static class UnityRandomPatches
        {
            [HarmonyPostfix]
            [HarmonyPatch("InitState")]
            static void InitStatePost(int seed) {
                instance.LogInfo("InitState(" + seed + ")");
                instance.LogRngCall();
            }

            [HarmonyPostfix]
            [HarmonyPatch("Range", new Type[] { typeof(float), typeof(float) })]
            static void FloatRangePost(float min, float max, float __result) {
                instance.LogInfo("FloatRange(" + min + ", " + max + ") = " + __result);
                instance.LogRngCall();
            }

            [HarmonyPostfix]
            [HarmonyPatch("Range", new Type[] { typeof(int), typeof(int) })]
            static void IntRangePost(int min, int max, int __result) {
                instance.LogInfo("IntRange(" + min + ", " + max + ") = " + __result);
                instance.LogRngCall();
            }

            //NB: UnityEngine.Random.State looks like this:
            /*
            [Serializable]
	        public struct State
	        {
                [SerializeField]
                private int s0;

                [SerializeField]
                private int s1;

                [SerializeField]
                private int s2;

                [SerializeField]
                private int s3;
            }
            */
            // [HarmonyPostfix]
            // [HarmonyPatch("state", MethodType.Getter)]
            // static void GetStatePost(UnityEngine.Random.State __result) {
            //     RandomStateWrapper state = __result;
            //     instance.LogInfo("GetState = " + state);
            // }
            // [HarmonyPostfix]
            // [HarmonyPatch("state", MethodType.Setter)]
            // static void SetStatePost(UnityEngine.Random.State value) {
            //     RandomStateWrapper state = value;
            //     instance.LogInfo("SetState(" + value + ")");
            // }

            [HarmonyPostfix]
            [HarmonyPatch("value", MethodType.Getter)]
            static void GetValuePost(float __result) {
                instance.LogInfo("GetValue = " + __result);
                instance.LogRngCall();
            }

        }

        [HarmonyPatch(typeof(Echodog.SaveManager))]
        private static class EchoDogSaveManagerPatches
        {
            //start logging rng calls after ImportantRandomStart, after it's done its thing
            [HarmonyPostfix]
            [HarmonyPatch("ImportantRandomStart")]
            static void PostImportantStart() { isCurrentlyImportantTime = true; }

            //stop logging rng calls *before* ImportantRandomEnd, since it does rng stuff that we don't want to see
            [HarmonyPrefix]
            [HarmonyPatch("ImportantRandomEnd")]
            static void PostImportantEnd() { isCurrentlyImportantTime = false; }

        }
    }
}
