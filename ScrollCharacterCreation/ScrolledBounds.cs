using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;

namespace ScrollCharacterCreation;
public class ScrolledBounds {
    private readonly static double padding = GuiElementItemSlotGridBase.unscaledSlotPadding;
    private readonly static double scrollMargin = 0.0;

    private readonly bool useScroll;
    private readonly bool useInset;
    private readonly double outerWidth;
    private readonly double outerHeight;
    private readonly double viewHeight;
    private bool reCompose = true;

    public readonly ElementBounds Inner;
    public readonly ElementBounds Outer;
    private readonly ElementBounds view;
    private readonly ElementBounds inset;
    private ElementBounds scroll;
    private GuiElementScrollbar scrollbar;
    private GuiComposer composer;

    public double Width => outerWidth;

    public double Height => outerHeight;

    public ScrolledBounds(ElementBounds outer, ElementBounds inner, double? insetMargin = null)
        : this(outer.fixedX, outer.fixedY, inner.fixedWidth, inner.fixedHeight, outer.fixedHeight, insetMargin) {}

    public ScrolledBounds(double x,
                          double y,
                          double innerWidth,
                          double innerHeight,
                          double viewHeight,
                          double? insetMargin = null) {
        useScroll = innerHeight > viewHeight;
        view = ElementBounds.Fixed(x, y, innerWidth, viewHeight);

        useInset = insetMargin != null;
        double margin = insetMargin ?? 0.0;
        this.viewHeight = viewHeight;
        outerHeight = viewHeight + 2 * margin;
        inset = view.ForkBoundingParent(margin, margin, margin, margin);

        Outer = inset.ForkBoundingParent(rightSpacing: useScroll ? 20.0 : 0.0);
        outerWidth = Outer.fixedWidth;
        Inner = useScroll ? ElementBounds.Fixed(0.0, 0.0, innerWidth, innerHeight).WithParent(Outer) : view;

        SetChildren(view, inset, Outer);
    }

    private static void SetChildren(params ElementBounds[] bounds) {
        for (int i = 0; i < bounds.Length - 1; i++) {
            if (!ReferenceEquals(bounds[i], bounds[i + 1])) {
                bounds[i].ChildBounds.Add(bounds[i + 1]);
            }
        }
    }

    public GuiComposer BeginScroll(GuiComposer composer, string key = null) {
        Outer.CalcWorldBounds();
        if (useInset) {
            composer.AddInset(inset);
        }
        if (useScroll) {
            composer
                .AddInteractiveElement(Scrollbar(composer.Api), key)
                .BeginClip(view);
        }
        return composer;
    }

    public GuiComposer EndScroll(GuiComposer composer) {
        if (useScroll) {
            composer.EndClip();
            this.composer = composer;
        }
        return composer;
    }

    public GuiElementScrollbar Scrollbar(ICoreClientAPI api) {
        scroll = ElementStdBounds.VerticalScrollbar(inset).WithParent(Outer);
        scrollbar = new GuiElementLimitedScrollbar(api, OnScroll, scroll, Outer);
        return scrollbar;
    }

    public void SetupScrollbar(bool last = true) {
        reCompose = last;
        scrollbar?.SetHeights((float) viewHeight, (float) (Inner.fixedHeight + padding));
        reCompose = true;
    }

    private void OnScroll(float value) {
        if (useScroll) {
            Inner.fixedY = scrollMargin - (double) value;
            Inner.CalcWorldBounds();
            if (reCompose) composer.ReCompose();
        }
    }

    private class GuiElementLimitedScrollbar : GuiElementScrollbar {
        private readonly ElementBounds limit;

        public GuiElementLimitedScrollbar(ICoreClientAPI capi,
                                          Action<float> onScroll,
                                          ElementBounds bounds,
                                          ElementBounds limit)
            : base(capi, onScroll, bounds) {
            this.limit = limit;
        }

        public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args) {
            if (limit.PointInside(api.Input.MouseX, api.Input.MouseY)) {
                base.OnMouseWheel(api, args);
            }
        }
    }
}
