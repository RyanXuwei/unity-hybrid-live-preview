using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RyanXuWei.HybridLivePreview.Accessibility
{
    // Tells the page's accessibility layer how to read this UI element. Buttons work without it (they read
    // their child text); images need it with a Label, otherwise they count as decoration and are skipped.
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Accessibility/A11y Label")]
    public sealed class A11yLabel : MonoBehaviour
    {
        public enum A11yRole
        {
            Auto,
            Button,
            Image,
            Text,
            Ignore,
        }

        [Tooltip("Auto: decided by the components (Button = button, Text/TMP = text, Image with a Label = image). Ignore: screen readers skip this element and its children.")]
        public A11yRole role = A11yRole.Auto;

        [Tooltip("Button name or image alt text. Empty: buttons read their child text.")]
        public string label;

        [Tooltip("Extra description, read after the name.")]
        [TextArea(1, 3)]
        public string description;

        [Tooltip("Whether images and text can be reached with Tab. Buttons always can.")]
        public bool tabStop = true;

        // The previous role tells us the role was just switched to Text/Button. roleTracked keeps existing
        // components from having their Label overwritten the first time they are validated.
        [SerializeField, HideInInspector]
        private A11yRole lastRole = A11yRole.Auto;

        [SerializeField, HideInInspector]
        private bool roleTracked;

        private void OnValidate()
        {
            bool switched = roleTracked && role != lastRole;
            if (switched && (role == A11yRole.Text || role == A11yRole.Button) && string.IsNullOrWhiteSpace(label))
            {
                FillLabelFromText();
            }

            lastRole = role;
            roleTracked = true;
        }

        // Copies the text of this object's (or a child's) TextMeshPro/Text into Label; keeps Label when there is none.
        [ContextMenu("Fill Label From Text")]
        private void FillLabelFromText()
        {
            TMP_Text tmp = GetComponentInChildren<TMP_Text>(true);
            if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            {
                label = tmp.text;
                return;
            }

            Text legacy = GetComponentInChildren<Text>(true);
            if (legacy != null && !string.IsNullOrWhiteSpace(legacy.text))
            {
                label = legacy.text;
            }
        }
    }
}
