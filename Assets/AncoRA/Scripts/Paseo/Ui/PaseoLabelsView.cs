using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AncorRA.AR
{
    public readonly struct PaseoLabelItem
    {
        public readonly string Text;
        public readonly Vector2? Position;

        public PaseoLabelItem(string text, Vector2? position)
        {
            Text = text;
            Position = position;
        }
    }

    /// <summary>
    /// Name pills pinned over the boxes. Positions are in panel coordinates (the pill's bottom centre); null hides the
    /// pill (box hidden or behind the camera). Pills are reused frame to frame.
    /// </summary>
    public sealed class PaseoLabelsView
    {
        readonly List<Label> pills = new();

        public VisualElement Root { get; }
        public IReadOnlyList<Label> Pills => pills;

        public PaseoLabelsView()
        {
            Root = new VisualElement { name = "nombres", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("paseo-labels");
        }

        /// <returns>True when a pill was created, so the caller applies the fonts to it.</returns>
        public bool Set(IReadOnlyList<PaseoLabelItem> items)
        {
            bool created = false;
            while (pills.Count < items.Count)
            {
                var pill = PaseoUiKit.Text("", "paseo-label", strong: true);
                pill.pickingMode = PickingMode.Ignore;
                pills.Add(pill);
                Root.Add(pill);
                created = true;
            }
            for (int i = 0; i < pills.Count; i++)
            {
                var pill = pills[i];
                bool show = i < items.Count && items[i].Position.HasValue;
                PaseoUiKit.SetHidden(pill, !show);
                if (!show)
                    continue;
                var position = items[i].Position.Value;
                pill.text = items[i].Text;
                pill.style.left = position.x;
                pill.style.top = position.y;
            }
            return created;
        }
    }
}
