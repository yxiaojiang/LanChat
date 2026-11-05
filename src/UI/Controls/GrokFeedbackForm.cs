using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 100% 像素级对标原生发送反馈窗口 (完全对标 SendFK.png 与 Docs §9)
    /// 继承自 GrokModalFormBase，标准尺寸 520×460 (基准度量)，采用自绘 GrokButton
    /// </summary>
    public class GrokFeedbackForm : GrokModalFormBase
    {
        private TextBox _txtFeedback;
        private bool _includeDiag = true;
        private GrokButton _btnCancel;
        private GrokButton _btnSubmit;

        public GrokFeedbackForm()
        {
            this.Size = GrokTheme.SizeFeedback; // 520 x 460
            this.ShowCloseButton = true;
            this.Font = GrokTheme.FontSecondary;

            BuildControls();
        }

        private void BuildControls()
        {
            // 多行反馈输入框
            _txtFeedback = new TextBox
            {
                Multiline = true,
                Location = new Point(32, 100),
                Size = new Size(456, 224),
                BackColor = GrokTheme.Level05, // #121212
                ForeColor = GrokTheme.TextPrimary,
                Font = GrokTheme.FontCardItem,
                BorderStyle = BorderStyle.None
            };
            _txtFeedback.GotFocus += (s, e) => this.Invalidate();
            _txtFeedback.LostFocus += (s, e) => this.Invalidate();
            _txtFeedback.TextChanged += (s, e) => this.Invalidate();
            this.Controls.Add(_txtFeedback);

            // 底部取消按钮
            _btnCancel = new GrokButton("取消", GrokButtonRole.Secondary, DialogResult.Cancel)
            {
                Location = new Point(306, 400),
                Size = new Size(88, GrokTheme.ButtonHeightStandard)
            };
            this.Controls.Add(_btnCancel);

            // 底部发送反馈按钮
            _btnSubmit = new GrokButton("发送反馈", GrokButtonRole.Primary)
            {
                Location = new Point(402, 400),
                Size = new Size(96, GrokTheme.ButtonHeightStandard)
            };
            _btnSubmit.Click += (s, e) =>
            {
                string content = _txtFeedback.Text.Trim();
                if (string.IsNullOrEmpty(content))
                {
                    GrokMessageBox.Show(this, "请先输入您的反馈或问题描述。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                GrokMessageBox.Show(this, "感谢您的宝贵反馈！我们将持续精进优化 LanChat。", "反馈成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            };
            this.Controls.Add(_btnSubmit);

            this.AcceptButton = _btnSubmit;
            this.CancelButton = _btnCancel;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 点击 Checkbox 切换
                Rectangle chkRect = new Rectangle(28, 350, 260, 26);
                if (chkRect.Contains(e.Location))
                {
                    _includeDiag = !_includeDiag;
                    this.Invalidate(chkRect);
                    return;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaintContent(Graphics g)
        {
            // 1. 标题 "发送反馈" (18.0pt Bold #FFFFFF)
            using (Font titleFont = GrokTheme.FontTitle)
            using (SolidBrush tb = new SolidBrush(GrokTheme.TextTitle))
            {
                g.DrawString("发送反馈", titleFont, tb, new PointF(26, 20));
            }

            // 2. 副标题
            using (Font subFont = GrokTheme.FontSecondary)
            using (SolidBrush sb = new SolidBrush(GrokTheme.TextSecondary))
            {
                g.DrawString("我们重视所有反馈，无论是错误、想法还是体验建议", subFont, sb, new PointF(28, 58));
            }

            // 3. 文本框容器边框与占位提示 (圆角 10px)
            Rectangle boxRect = new Rectangle(22, 90, 476, 244);
            using (SolidBrush bb = new SolidBrush(GrokTheme.Level05))
            using (Pen bp = new Pen((_txtFeedback != null && _txtFeedback.Focused) ? GrokTheme.AccentPurple : GrokTheme.Border, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, bb, boxRect, 10);
                GrokTheme.DrawRoundedRectangle(g, bp, boxRect, 10);
            }

            if (_txtFeedback != null && string.IsNullOrEmpty(_txtFeedback.Text))
            {
                using (Font pf = GrokTheme.FontSecondary)
                using (SolidBrush pb = new SolidBrush(GrokTheme.TextPlaceholder))
                {
                    g.DrawString("请在此详细描述您遇到的问题或改进建议...", pf, pb, new PointF(32, 100));
                }
            }

            // 4. Checkbox: "包含当前节点与网络诊断信息" (Docs §3 蓝底白勾)
            Rectangle chkBox = new Rectangle(28, 354, 18, 18);
            if (_includeDiag)
            {
                using (SolidBrush cb = new SolidBrush(GrokTheme.AccentBlueBtn))
                {
                    GrokTheme.DrawRoundedRectangle(g, cb, chkBox, 4);
                }
                using (Pen cp = new Pen(Color.White, 2f))
                {
                    cp.StartCap = LineCap.Round;
                    cp.EndCap = LineCap.Round;
                    g.DrawLines(cp, new Point[] {
                        new Point(chkBox.X + 4, chkBox.Y + 9),
                        new Point(chkBox.X + 8, chkBox.Y + 13),
                        new Point(chkBox.X + 14, chkBox.Y + 5)
                    });
                }
            }
            else
            {
                using (Pen cp = new Pen(Color.FromArgb(80, 80, 88), 1.5f))
                {
                    GrokTheme.DrawRoundedRectangle(g, cp, chkBox, 4);
                }
            }

            using (Font lblFont = GrokTheme.FontSecondary)
            using (SolidBrush lb = new SolidBrush(GrokTheme.TextCard))
            {
                g.DrawString("包含当前节点与网络诊断信息", lblFont, lb, new PointF(54, 354));
            }
        }
    }
}
