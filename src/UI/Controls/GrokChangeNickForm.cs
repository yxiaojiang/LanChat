using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// GrokBot 现代暗黑清爽风格昵称修改窗口
    /// 继承自 GrokModalFormBase，标准尺寸 440×260 (基准度量)，采用自绘 GrokButton
    /// </summary>
    public class GrokChangeNickForm : GrokModalFormBase
    {
        private TextBox _txtNick;
        private GrokButton _btnCancel;
        private GrokButton _btnSave;

        /// <summary>获取保存成功后的新昵称</summary>
        public string NewNickName { get; private set; }

        /// <summary>
        /// 初始化修改昵称模态对话框
        /// </summary>
        /// <param name="currentNick">当前使用的原始昵称</param>
        public GrokChangeNickForm(string currentNick)
        {
            NewNickName = currentNick ?? string.Empty;

            this.Size = GrokTheme.SizeChangeNick; // 440 x 260
            this.ShowCloseButton = true;
            this.Font = GrokTheme.FontSecondary;

            BuildControls(currentNick);
        }

        private void BuildControls(string currentNick)
        {
            _txtNick = new TextBox
            {
                Text = currentNick,
                Location = new Point(36, 134),
                Size = new Size(368, 26),
                BackColor = GrokTheme.Level05, // #121212
                ForeColor = GrokTheme.TextPrimary,
                Font = GrokTheme.FontCardItem,
                BorderStyle = BorderStyle.None
            };
            _txtNick.GotFocus += (s, e) => this.Invalidate();
            _txtNick.LostFocus += (s, e) => this.Invalidate();
            this.Controls.Add(_txtNick);

            // 底部操作按钮
            _btnCancel = new GrokButton("取消", GrokButtonRole.Secondary, DialogResult.Cancel)
            {
                Location = new Point(236, 196),
                Size = new Size(84, GrokTheme.ButtonHeightStandard)
            };
            this.Controls.Add(_btnCancel);

            _btnSave = new GrokButton("保存", GrokButtonRole.Primary)
            {
                Location = new Point(328, 196),
                Size = new Size(84, GrokTheme.ButtonHeightStandard)
            };
            _btnSave.Click += (s, e) =>
            {
                string text = _txtNick.Text.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    GrokMessageBox.Show(this, "昵称不能为空，请输入有效的名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                NewNickName = text;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(_btnSave);

            this.AcceptButton = _btnSave;
            this.CancelButton = _btnCancel;
        }

        protected override void OnPaintContent(Graphics g)
        {
            // 1. 标题 "修改我的昵称" (18.0pt Bold #FFFFFF)
            using (Font titleFont = GrokTheme.FontTitle)
            using (SolidBrush tb = new SolidBrush(GrokTheme.TextTitle))
            {
                g.DrawString("修改我的昵称", titleFont, tb, new PointF(24, 20));
            }

            // 2. 吉祥物头像徽标 (38x38) 与提示文字
            Rectangle avRect = new Rectangle(26, 68, 38, 38);
            WeChatIcon.DrawMascotAvatarShape(g, avRect, 0, GrokTheme.AccentCoral, true, GrokTheme.Level1);

            using (Font tipFont = GrokTheme.FontSecondary)
            using (SolidBrush sb = new SolidBrush(GrokTheme.TextSecondary))
            {
                g.DrawString("在局域网内向其他节点展示您的身份名称:", tipFont, sb, new PointF(74, 78));
            }

            // 3. 输入框暗黑容器 (高 42px, 圆角 8px)
            Rectangle boxRect = new Rectangle(24, 126, 392, 42);
            using (SolidBrush bb = new SolidBrush(GrokTheme.Level05))
            using (Pen bp = new Pen((_txtNick != null && _txtNick.Focused) ? GrokTheme.AccentPurple : GrokTheme.Border, 1f))
            {
                GrokTheme.DrawRoundedRectangle(g, bb, boxRect, 8);
                GrokTheme.DrawRoundedRectangle(g, bp, boxRect, 8);
            }
        }
    }
}
