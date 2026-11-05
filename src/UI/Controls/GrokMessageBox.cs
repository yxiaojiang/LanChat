using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 现代暗黑清爽风格统一弹窗提示框 (彻底取代系统生硬白底 MessageBox)
    /// 继承自 GrokModalFormBase，标准尺寸 460×260 (基准度量)，采用自绘 GrokButton
    /// </summary>
    public class GrokMessageBox : GrokModalFormBase
    {
        private readonly MessageBoxIcon _iconType;
        private readonly string _title;
        private readonly string _message;

        /// <summary>
        /// 初始化自定义暗黑模态提示框
        /// </summary>
        /// <param name="message">提示消息正文</param>
        /// <param name="title">弹窗左上角小标题</param>
        /// <param name="buttons">按钮组合枚举 (OK / OKCancel / YesNo / YesNoCancel)</param>
        /// <param name="icon">状态指示图标 (Information / Warning / Error / Question)</param>
        public GrokMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            _message = message ?? string.Empty;
            _title = string.IsNullOrEmpty(title) ? "提示" : title;
            _iconType = icon;

            this.ShowCloseButton = true;
            this.Font = GrokTheme.FontSecondary;

            BuildLayout(buttons);
        }

        private void BuildLayout(MessageBoxButtons buttons)
        {
            // 计算文本合适尺寸
            int baseW = GrokTheme.SizeMessageBox.Width; // 460
            int maxTextW = baseW - 96; // 364
            Size textSize;
            using (Font f = GrokTheme.FontSecondary)
            {
                textSize = TextRenderer.MeasureText(_message, f, new Size(maxTextW, int.MaxValue), TextFormatFlags.WordBreak);
            }

            int contentH = Math.Max(textSize.Height + 10, 50);
            int formH = Math.Max(GrokTheme.SizeMessageBox.Height, 56 + contentH + 70);
            this.Size = new Size(baseW, formH);

            // 文本区域
            Label lbl = new Label
            {
                Text = _message,
                Location = new Point(72, 60),
                Size = new Size(maxTextW, contentH),
                ForeColor = GrokTheme.TextPrimary,
                BackColor = Color.Transparent,
                Font = GrokTheme.FontSecondary
            };
            this.Controls.Add(lbl);

            // 底部操作按钮容器
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(20, 10, 24, 10),
                BackColor = Color.Transparent
            };

            switch (buttons)
            {
                case MessageBoxButtons.YesNo:
                    GrokButton btnNo = new GrokButton("否", GrokButtonRole.Secondary, DialogResult.No) { IsPill = true, Width = 84, Margin = new Padding(8, 0, 0, 0) };
                    GrokButton btnYes = new GrokButton("是", GrokButtonRole.Primary, DialogResult.Yes) { IsPill = true, Width = 84, Margin = new Padding(8, 0, 0, 0) };
                    flow.Controls.Add(btnNo);
                    flow.Controls.Add(btnYes);
                    this.AcceptButton = btnYes;
                    this.CancelButton = btnNo;
                    break;

                case MessageBoxButtons.OKCancel:
                    GrokButton btnCancel = new GrokButton("取消", GrokButtonRole.Secondary, DialogResult.Cancel) { IsPill = true, Width = 84, Margin = new Padding(8, 0, 0, 0) };
                    GrokButton btnOk2 = new GrokButton("确定", GrokButtonRole.Primary, DialogResult.OK) { IsPill = true, Width = 84, Margin = new Padding(8, 0, 0, 0) };
                    flow.Controls.Add(btnCancel);
                    flow.Controls.Add(btnOk2);
                    this.AcceptButton = btnOk2;
                    this.CancelButton = btnCancel;
                    break;

                case MessageBoxButtons.YesNoCancel:
                    GrokButton btnCancel2 = new GrokButton("取消", GrokButtonRole.Secondary, DialogResult.Cancel) { IsPill = true, Width = 84, Margin = new Padding(8, 0, 0, 0) };
                    GrokButton btnNo2 = new GrokButton("否", GrokButtonRole.Secondary, DialogResult.No) { IsPill = true, Width = 84, Margin = new Padding(8, 0, 0, 0) };
                    GrokButton btnYes2 = new GrokButton("是", GrokButtonRole.Primary, DialogResult.Yes) { IsPill = true, Width = 84, Margin = new Padding(8, 0, 0, 0) };
                    flow.Controls.Add(btnCancel2);
                    flow.Controls.Add(btnNo2);
                    flow.Controls.Add(btnYes2);
                    this.AcceptButton = btnYes2;
                    this.CancelButton = btnCancel2;
                    break;

                default: // OK
                    GrokButton btnOk = new GrokButton("确定", GrokButtonRole.Primary, DialogResult.OK) { IsPill = true, Width = 88, Margin = new Padding(8, 0, 0, 0) };
                    flow.Controls.Add(btnOk);
                    this.AcceptButton = btnOk;
                    break;
            }

            this.Controls.Add(flow);
        }

        protected override void OnPaintContent(Graphics g)
        {
            // 顶部小标题 (10.5pt Bold)
            using (Font titleFont = GrokTheme.FontSectionTitle)
            using (SolidBrush tb = new SolidBrush(GrokTheme.TextTitle))
            {
                g.DrawString(_title, titleFont, tb, new PointF(24, 18));
            }

            // 左侧状态矢量图标
            DrawStatusIcon(g, new Rectangle(24, 58, 36, 36));
        }

        private void DrawStatusIcon(Graphics g, Rectangle r)
        {
            switch (_iconType)
            {
                case MessageBoxIcon.Error:
                    using (SolidBrush eb = new SolidBrush(GrokTheme.AccentCoral))
                    {
                        g.FillEllipse(eb, r);
                    }
                    using (Pen ep = new Pen(Color.White, 2.2f))
                    {
                        ep.StartCap = LineCap.Round;
                        ep.EndCap = LineCap.Round;
                        int pad = 9;
                        g.DrawLine(ep, r.X + pad, r.Y + pad, r.Right - pad, r.Bottom - pad);
                        g.DrawLine(ep, r.Right - pad, r.Y + pad, r.X + pad, r.Bottom - pad);
                    }
                    break;

                case MessageBoxIcon.Warning:
                    using (SolidBrush wb = new SolidBrush(Color.FromArgb(255, 160, 26)))
                    {
                        using (GraphicsPath wp = new GraphicsPath())
                        {
                            wp.AddLine(r.X + r.Width / 2f, r.Y + 2, r.Right - 2, r.Bottom - 2);
                            wp.AddLine(r.Right - 2, r.Bottom - 2, r.X + 2, r.Bottom - 2);
                            wp.CloseFigure();
                            g.FillPath(wb, wp);
                        }
                    }
                    using (SolidBrush exb = new SolidBrush(GrokTheme.Level1))
                    {
                        g.FillRectangle(exb, r.X + r.Width / 2f - 1.2f, r.Y + 12, 2.4f, 9f);
                        g.FillEllipse(exb, r.X + r.Width / 2f - 1.5f, r.Y + 23, 3f, 3f);
                    }
                    break;

                case MessageBoxIcon.Question:
                    WeChatIcon.DrawMascotAvatarShape(g, r, 0, Color.FromArgb(144, 90, 251), false, Color.Transparent);
                    break;

                default: // Information
                    WeChatIcon.DrawMascotAvatarShape(g, r, 0, Color.FromArgb(77, 155, 255), false, Color.Transparent);
                    break;
            }
        }

        #region Static Helpers

        /// <summary>快捷弹出仅包含“确定”按钮的信息提示框</summary>
        /// <param name="text">提示文案</param>
        /// <returns>用户交互结果</returns>
        public static DialogResult Show(string text)
        {
            return Show(null, text, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>快捷弹出指定标题与正文的信息提示框</summary>
        /// <param name="text">提示文案</param>
        /// <param name="title">弹窗标题</param>
        /// <returns>用户交互结果</returns>
        public static DialogResult Show(string text, string title)
        {
            return Show(null, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>快捷弹出指定按钮与状态图标的提示框</summary>
        /// <param name="text">提示文案</param>
        /// <param name="title">弹窗标题</param>
        /// <param name="buttons">按钮选项</param>
        /// <param name="icon">状态图标</param>
        /// <returns>用户交互结果</returns>
        public static DialogResult Show(string text, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(null, text, title, buttons, icon);
        }

        /// <summary>
        /// 完整调用：在指定父窗口居中弹出统一样式自绘暗黑模态提示框
        /// </summary>
        /// <param name="owner">父级窗体</param>
        /// <param name="text">提示文案</param>
        /// <param name="title">弹窗标题</param>
        /// <param name="buttons">按钮选项</param>
        /// <param name="icon">状态图标</param>
        /// <returns>用户点击的按钮返回结果</returns>
        public static DialogResult Show(IWin32Window owner, string text, string title = "提示", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            using (GrokMessageBox box = new GrokMessageBox(text, title, buttons, icon))
            {
                Form parentForm = owner as Form ?? Form.ActiveForm;
                if (parentForm != null && parentForm.Visible)
                {
                    box.StartPosition = FormStartPosition.CenterParent;
                    return box.ShowDialog(parentForm);
                }
                else
                {
                    box.StartPosition = FormStartPosition.CenterScreen;
                    return box.ShowDialog();
                }
            }
        }

        #endregion
    }
}
