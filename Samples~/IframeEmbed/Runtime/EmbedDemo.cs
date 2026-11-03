using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RyanXuWei.HybridLivePreview.IframeEmbed
{
    // The Unity half of the Iframe Embed sample. The game state lives here; every page only shows it and asks
    // for changes. The page inside the iframe (your H5 UI) and the parent page around it (embed/host.html)
    // receive the same "embed:state" messages, because enableParentRelay() forwards them, and their
    // requests ("embed:get", "embed:pause", "embed:reset") arrive here in the same way.
    //
    // It creates itself after the first scene loads (when the scene has none), so there is nothing to set up.
    // Delete the imported sample folder to remove it.
    public class EmbedDemo : MonoBehaviour
    {
        [Serializable]
        struct State
        {
            public int score;
            public bool paused;
            public string scene;
        }

        [Serializable]
        struct PauseRequest
        {
            public bool paused;
        }

        [Tooltip("Score points added per second while not paused.")]
        public float pointsPerSecond = 1f;

        State state;
        float pending;
        IDisposable[] subscriptions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (FindAnyObjectByType<EmbedDemo>() != null)
                return;
            var go = new GameObject(nameof(EmbedDemo));
            DontDestroyOnLoad(go);
            go.AddComponent<EmbedDemo>();
        }

        void OnEnable()
        {
            subscriptions = new[]
            {
                // A page that just loaded asks for the current state; Unity answers instead of the page guessing.
                HybridBridge.OnJson("embed:get", _ => Publish()),
                HybridBridge.On<PauseRequest>("embed:pause", request =>
                {
                    state.paused = request.paused;
                    Publish();
                }),
                HybridBridge.OnJson("embed:reset", _ =>
                {
                    state.score = 0;
                    pending = 0;
                    Publish();
                }),
            };
            Publish();
        }

        void OnDisable()
        {
            foreach (var subscription in subscriptions)
                subscription.Dispose();
        }

        void Update()
        {
            if (state.paused)
                return;
            pending += pointsPerSecond * Time.unscaledDeltaTime;
            if (pending < 1f)
                return;
            int points = Mathf.FloorToInt(pending);
            pending -= points;
            state.score += points;
            Publish();
        }

        void Publish()
        {
            state.scene = SceneManager.GetActiveScene().name;
            HybridBridge.Send("embed:state", state);
        }
    }
}
