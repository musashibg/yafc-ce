using System;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public abstract class Window : WidgetContainer, IPanel, IGui
    {
        public readonly UiBatch rootBatch;
        public readonly ImGui rootGui;
        internal IntPtr window;
        internal IntPtr renderer;
        internal Vector2 contentSize;
        internal uint id;
        internal bool repaintRequired = true;
        internal bool visible;
        internal long nextRepaintTime = long.MaxValue;
        internal static RenderingUtils.BlitMapping[] blitMapping;
        internal float pixelsPerUnit;

        public override SchemeColor boxColor => SchemeColor.Background;
        
        public int displayIndex => SDL.SDL_GetWindowDisplayIndex(window);

        public Vector2 size => contentSize;

        internal Window()
        {
            padding = new Padding(5f, 2f);
            rootBatch = new UiBatch(this);
            rootGui = new ImGui(this);
        }
        
        internal void Create()
        {
            SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            id = SDL.SDL_GetWindowID(window);
            Ui.RegisterWindow(id, this);
            visible = true;
        }

        internal float UnitsToPixelsFromDpi(float dpi) => dpi == 0 ? 13 : MathUtils.Round(dpi / 6.8f);

        internal virtual void WindowResize()
        {
            rootGui.Rebuild();
        }

        internal void WindowMoved()
        {
            var index = SDL.SDL_GetWindowDisplayIndex(window);
            SDL.SDL_GetDisplayDPI(index, out var ddpi, out _, out _);
            var u2p = UnitsToPixelsFromDpi(ddpi);
            if (u2p != pixelsPerUnit)
            {
                pixelsPerUnit = u2p;
                repaintRequired = true;
                rootGui.MarkEverythingForRebuild();
            }
        }

        internal void Render()
        {
            if (!repaintRequired && nextRepaintTime > Ui.time)
                return;
            nextRepaintTime = long.MaxValue;
            repaintRequired = false;
            if (rootGui.IsRebuildRequired())
                rootGui.Build(new Rect(default, size), null, pixelsPerUnit);

            MainRender();
        }

        internal virtual void MainRender()
        {
            var bgColor = boxColor.ToSdlColor();
            SDL.SDL_SetRenderDrawColor(renderer, bgColor.r,bgColor.g,bgColor.b, bgColor.a);
            var fullRect = new Rect(default, contentSize);
            {
                // TODO work-around sdl bug
                var clip = rootGui.ToSdlRect(fullRect);
                SDL.SDL_RenderSetClipRect(renderer, ref clip);
            }
            SDL.SDL_RenderClear(renderer);
            rootGui.Present(this, fullRect, fullRect);
            SDL.SDL_RenderPresent(renderer);
        }

        [Obsolete]
        public bool HitTest<T>(Vector2 position, out HitTestResult<T> result) where T : class, IMouseHandleBase => rootBatch.HitTest<T>(position, out result);
        public IGuiPanel HitTest(Vector2 position) => rootGui.HitTest(position);

        public Vector2 BuildPanel(UiBatch batch, Vector2 size)
        {
            var state = new LayoutState(batch, size.X, RectAllocator.Stretch);
            Build(state);
            return state.size;
        }

        internal abstract void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color);
        internal abstract void DrawBorder(SDL.SDL_Rect position, RectangleBorder border);

        public void Repaint()
        {
            if (!Ui.IsMainThread())
                throw new NotSupportedException("This should be called from the main thread");
            repaintRequired = true;
        }

        protected internal virtual void Close()
        {
            visible = false;
            SDL.SDL_DestroyWindow(window);
            window = renderer = IntPtr.Zero;
            Ui.UnregisterWindow(this);
        }

        private void Focus()
        {
            if (window != IntPtr.Zero)
            {
                SDL.SDL_RaiseWindow(window);
                SDL.SDL_RestoreWindow(window);
                SDL.SDL_SetWindowInputFocus(window);
            }
        }

        
        public virtual void FocusLost() {}

        public void SetNextRepaint(long nextRepaintTime)
        {
            if (this.nextRepaintTime > nextRepaintTime)
                this.nextRepaintTime = nextRepaintTime;
        }

        public virtual void Build(ImGui gui) {}
    }
}