namespace RyanXuWei.HybridLivePreview.Accessibility
{
    // Subtitles: shown on the page as HTML captions and read aloud through an aria-live region.
    //   A11ySubtitle.Show("Welcome to the village", "Elder");
    public static class A11ySubtitle
    {
        [System.Serializable]
        private struct Line
        {
            public string text;
            public string speaker;
            public float seconds;
            public bool urgent;
        }

        // seconds <= 0 keeps the line up until the next one or Clear(). urgent interrupts what the reader is saying.
        public static void Show(string text, string speaker = null, float seconds = 4f, bool urgent = false)
        {
            HybridBridge.Send("a11y:subtitle", new Line { text = text, speaker = speaker ?? "", seconds = seconds, urgent = urgent });
        }

        public static void Clear()
        {
            HybridBridge.Send("a11y:subtitle", new Line { text = "" });
        }
    }
}
