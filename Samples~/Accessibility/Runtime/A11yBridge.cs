using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RyanXuWei.HybridLivePreview.Accessibility
{
    // Sends the on-screen uGUI buttons, images and text to the page as an accessibility tree (a11y:tree); the page
    // lays transparent real DOM elements over the game for screen readers. a11y:click presses a button through
    // the same pointerClickHandler a real click uses; a11y:focus keeps the EventSystem selection in step.
    // Created automatically after the first scene loads and kept across scene loads.
    public sealed class A11yBridge : MonoBehaviour
    {
        [Serializable]
        private struct Node
        {
            public int id;
            public string role;
            public string label;
            public string description;
            public bool disabled;
            public bool focusable;
            public float x;
            public float y;
            public float w;
            public float h;
        }

        [Serializable]
        private struct Tree
        {
            public List<Node> nodes;
        }

#pragma warning disable 0649 // filled by JsonUtility
        [Serializable]
        private struct Target
        {
            public int id;
        }
#pragma warning restore 0649

        private const float ScanInterval = 0.2f;

        private readonly List<Node> nodes = new List<Node>();
        private readonly Dictionary<int, GameObject> lookup = new Dictionary<int, GameObject>();
        private readonly Vector3[] corners = new Vector3[4];
        private IDisposable clickSubscription;
        private IDisposable focusSubscription;
        private IDisposable readySubscription;
        private string lastJson;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (FindFirstObjectByType<A11yBridge>() != null)
            {
                return;
            }

            GameObject bridgeObject = new GameObject(nameof(A11yBridge));
            DontDestroyOnLoad(bridgeObject);
            bridgeObject.AddComponent<A11yBridge>();
        }

        private void OnEnable()
        {
            clickSubscription = HybridBridge.On<Target>("a11y:click", target => Click(target.id));
            focusSubscription = HybridBridge.On<Target>("a11y:focus", target => Focus(target.id));
            // A reloaded page asks for the whole tree again.
            readySubscription = HybridBridge.OnJson("a11y:ready", _ => lastJson = null);
        }

        private void OnDisable()
        {
            clickSubscription?.Dispose();
            focusSubscription?.Dispose();
            readySubscription?.Dispose();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan)
            {
                return;
            }

            nextScan = Time.unscaledTime + ScanInterval;
            Scan();
            string json = JsonUtility.ToJson(new Tree { nodes = nodes });
            if (json != lastJson)
            {
                lastJson = json;
                HybridBridge.SendJson("a11y:tree", json);
            }
        }

        private void Scan()
        {
            nodes.Clear();
            lookup.Clear();

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Array.Sort(canvases, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            foreach (Canvas canvas in canvases)
            {
                if (!canvas.isRootCanvas || !canvas.isActiveAndEnabled)
                {
                    continue;
                }

                Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                CollectFrom(canvas.transform, eventCamera);
            }
        }

        // Collected in Hierarchy order, so the page's Tab order follows the screen's structure.
        private void CollectFrom(Transform parent, Camera eventCamera)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (!child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                A11yLabel tag = child.GetComponent<A11yLabel>();
                if (tag != null && tag.role == A11yLabel.A11yRole.Ignore)
                {
                    continue;
                }

                if (TryDescribe(child, tag, out string role, out string label, out bool disabled))
                {
                    if (TryGetScreenRect(child as RectTransform, eventCamera, out Rect rect))
                    {
                        int id = child.gameObject.GetInstanceID();
                        lookup[id] = child.gameObject;
                        nodes.Add(new Node
                        {
                            id = id,
                            role = role,
                            label = label,
                            description = tag != null ? tag.description ?? "" : "",
                            disabled = disabled,
                            focusable = role != "button" && tag != null && tag.tabStop,
                            x = rect.x,
                            y = rect.y,
                            w = rect.width,
                            h = rect.height,
                        });
                    }

                    // The text inside a button is already its name; don't read it again.
                    if (role == "button")
                    {
                        continue;
                    }
                }

                CollectFrom(child, eventCamera);
            }
        }

        private static bool TryDescribe(Transform target, A11yLabel tag, out string role, out string label, out bool disabled)
        {
            role = null;
            label = null;
            disabled = false;
            A11yLabel.A11yRole forced = tag != null ? tag.role : A11yLabel.A11yRole.Auto;
            string tagLabel = tag != null ? tag.label : null;

            Selectable selectable = target.GetComponent<Selectable>();
            if (forced == A11yLabel.A11yRole.Button || (forced == A11yLabel.A11yRole.Auto && selectable is Button))
            {
                role = "button";
                label = !string.IsNullOrWhiteSpace(tagLabel) ? tagLabel : FirstText(target);
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = target.name;
                }

                disabled = selectable != null && !selectable.IsInteractable();
                return true;
            }

            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null && (!graphic.enabled || graphic.canvasRenderer.GetInheritedAlpha() < 0.01f))
            {
                return false;
            }

            string text = TextOf(target);
            if (forced == A11yLabel.A11yRole.Text || (forced == A11yLabel.A11yRole.Auto && text != null))
            {
                role = "text";
                label = !string.IsNullOrWhiteSpace(tagLabel) ? tagLabel : text;
                return !string.IsNullOrWhiteSpace(label);
            }

            // Images without alt text are decoration; screen readers skip them.
            if ((forced == A11yLabel.A11yRole.Image || forced == A11yLabel.A11yRole.Auto) && !string.IsNullOrWhiteSpace(tagLabel))
            {
                role = "image";
                label = tagLabel;
                return true;
            }

            return false;
        }

        private static string TextOf(Transform target)
        {
            TMP_Text tmp = target.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                return tmp.text;
            }

            Text legacy = target.GetComponent<Text>();
            return legacy != null ? legacy.text : null;
        }

        private static string FirstText(Transform target)
        {
            TMP_Text tmp = target.GetComponentInChildren<TMP_Text>();
            if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            {
                return tmp.text;
            }

            Text legacy = target.GetComponentInChildren<Text>();
            return legacy != null ? legacy.text : null;
        }

        // 0..1 fractions from the top-left of the screen, so the page can lay them straight over the game view.
        private bool TryGetScreenRect(RectTransform rectTransform, Camera eventCamera, out Rect rect)
        {
            rect = default;
            if (rectTransform == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return false;
            }

            rectTransform.GetWorldCorners(corners);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(eventCamera, corner);
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            if (maxX <= 0f || maxY <= 0f || minX >= Screen.width || minY >= Screen.height || maxX - minX < 1f || maxY - minY < 1f)
            {
                return false;
            }

            float Round(float v) => Mathf.Round(v * 10000f) / 10000f;
            rect = new Rect(
                Round(minX / Screen.width),
                Round(1f - maxY / Screen.height),
                Round((maxX - minX) / Screen.width),
                Round((maxY - minY) / Screen.height));
            return true;
        }

        private void Click(int id)
        {
            if (!lookup.TryGetValue(id, out GameObject target) || target == null || EventSystem.current == null)
            {
                return;
            }

            Selectable selectable = target.GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable())
            {
                return;
            }

            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target, eventData, ExecuteEvents.pointerClickHandler);
        }

        private void Focus(int id)
        {
            if (lookup.TryGetValue(id, out GameObject target) && target != null && EventSystem.current != null
                && target.GetComponent<Selectable>() != null)
            {
                EventSystem.current.SetSelectedGameObject(target);
            }
        }
    }
}
