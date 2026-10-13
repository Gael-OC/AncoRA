using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    /// <summary>Small helpers shared by the tour views: labels with classes, hiding, and the two Inter weights.</summary>
    public static class PaseoUiKit
    {
        public const string HiddenClass = "is-hidden";
        public const string StrongClass = "is-strong";

        public static Label Text(string text, string className, bool strong = false)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            if (strong)
                label.AddToClassList(StrongClass);
            return label;
        }

        public static void SetHidden(VisualElement element, bool hidden) => element.EnableInClassList(HiddenClass, hidden);

        public static bool IsHidden(VisualElement element) => element.ClassListContains(HiddenClass);

        /// <summary>
        /// Sets Inter on every text element below root (semibold on the ones marked strong). Inline styles win over the
        /// default theme, which sets its own font on text elements. Call again after adding text elements.
        /// </summary>
        public static void ApplyFonts(VisualElement root, Font regular, Font strong)
        {
            if (regular == null || strong == null)
                return;
            var regularDefinition = new StyleFontDefinition(FontDefinition.FromFont(regular));
            var strongDefinition = new StyleFontDefinition(FontDefinition.FromFont(strong));
            root.Query<TextElement>().ForEach(e =>
                e.style.unityFontDefinition = e.ClassListContains(StrongClass) ? strongDefinition : regularDefinition);
        }
    }
}
