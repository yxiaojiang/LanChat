using System;
using System.Drawing;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// Owner-draw session ListBox with double-buffering to avoid selection flicker.
    /// C# 5 / .NET Framework 4.0 friendly.
    /// </summary>
    public class SessionListBox : ListBox
    {
        private const int WS_EX_COMPOSITED = 0x02000000;

        public SessionListBox()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Composited paint for this list only — reduces selection flash with owner-draw.
                cp.ExStyle |= WS_EX_COMPOSITED;
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Suppress default erase; OnPaint fills the background (avoids flash).
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(this.BackColor))
            {
                g.FillRectangle(bg, this.ClientRectangle);
            }

            int count = this.Items.Count;
            if (count == 0 || this.ItemHeight <= 0) return;

            for (int i = 0; i < count; i++)
            {
                Rectangle itemRect;
                try
                {
                    itemRect = this.GetItemRectangle(i);
                }
                catch
                {
                    continue;
                }

                if (itemRect.Bottom < 0 || itemRect.Top > this.ClientSize.Height)
                    continue;
                if (!e.ClipRectangle.IntersectsWith(itemRect))
                    continue;

                DrawItemState state = DrawItemState.Default;
                if (i == this.SelectedIndex)
                    state |= DrawItemState.Selected;
                if (this.Focused && i == this.SelectedIndex)
                    state |= DrawItemState.Focus;

                DrawItemEventArgs dea = new DrawItemEventArgs(
                    g, this.Font, itemRect, i, state, this.ForeColor, this.BackColor);
                this.OnDrawItem(dea);
            }
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            // With UserPaint, force a clean repaint of visible rows after selection change.
            this.Invalidate();
        }
    }
}
