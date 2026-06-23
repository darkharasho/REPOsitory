using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace ColorblindMode
{
    /// <summary>
    /// Lives on the plugin GameObject (persists across levels). Lazily creates our OWN global
    /// PostProcessVolume holding a single ColorGrading override, placed on the same layer as the
    /// game's volume so the existing PostProcessLayer blends it on top, at a higher priority. We
    /// only override ColorGrading's nine channel-mixer fields, so the game's own grading is
    /// untouched. The mixer fields are a 3x3 matrix; we feed them from ColorMatrices.
    /// </summary>
    public class ColorblindController : MonoBehaviour
    {
        private PostProcessVolume? _volume;
        private ColorGrading? _grading;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Plugin.Type.SettingChanged += OnSettingChanged;
            Plugin.Intensity.SettingChanged += OnSettingChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Plugin.Type.SettingChanged -= OnSettingChanged;
            Plugin.Intensity.SettingChanged -= OnSettingChanged;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();
        private void OnSettingChanged(object sender, System.EventArgs e) => Apply();

        private void Update()
        {
            // The game's PostProcessing singleton isn't ready immediately; build once it exists.
            if (_volume == null && PostProcessing.Instance != null && PostProcessing.Instance.volume != null)
                Apply();
        }

        private void Apply()
        {
            try
            {
                if (!EnsureVolume()) return;

                var type = Plugin.Type.Value;
                bool on = type != ColorblindType.Off;
                _volume!.enabled = on;
                _grading!.enabled.Override(on);
                if (!on) return;

                float[] pct = ColorMatrices.ToMixerPercent(type, Plugin.Intensity.Value);
                _grading.gradingMode.Override(GradingMode.LowDefinitionRange);
                Set(_grading.mixerRedOutRedIn,    pct[0]);
                Set(_grading.mixerRedOutGreenIn,  pct[1]);
                Set(_grading.mixerRedOutBlueIn,   pct[2]);
                Set(_grading.mixerGreenOutRedIn,  pct[3]);
                Set(_grading.mixerGreenOutGreenIn,pct[4]);
                Set(_grading.mixerGreenOutBlueIn, pct[5]);
                Set(_grading.mixerBlueOutRedIn,   pct[6]);
                Set(_grading.mixerBlueOutGreenIn, pct[7]);
                Set(_grading.mixerBlueOutBlueIn,  pct[8]);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"ColorblindController.Apply: {ex}");
            }
        }

        private static void Set(FloatParameter p, float value)
        {
            p.overrideState = true;
            p.value = value;
        }

        private bool EnsureVolume()
        {
            if (_volume != null && _grading != null) return true;

            var gameVol = PostProcessing.Instance != null ? PostProcessing.Instance.volume : null;
            if (gameVol == null) return false;

            int layer = gameVol.gameObject.layer;          // same layer => existing PostProcessLayer sees it
            _grading = ScriptableObject.CreateInstance<ColorGrading>();
            _grading.enabled.Override(true);
            // Priority above the game's so our mixer wins where it overrides.
            _volume = PostProcessManager.instance.QuickVolume(layer, gameVol.priority + 1000f, _grading);
            _volume.isGlobal = true;
            _volume.weight = 1f;
            Object.DontDestroyOnLoad(_volume.gameObject);
            Plugin.Log.LogInfo($"[ColorblindMode] volume created on layer {layer}.");
            return true;
        }
    }
}
