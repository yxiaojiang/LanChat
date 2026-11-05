using System;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置子面板轻量依赖注入上下文 DTO，统一解耦宿主依赖
    /// </summary>
    public class SettingsContext
    {
        public NetworkService NetService { get; set; }
        public Action OnCheckUpdate { get; set; }
        public Action OnShowChangeNick { get; set; }
        public Action OnOpenFolder { get; set; }
        public Action OnFontChanged { get; set; }
        public IWin32Window OwnerWindow { get; set; }

        public SettingsContext(
            NetworkService netService,
            Action onCheckUpdate,
            Action onShowChangeNick,
            Action onOpenFolder,
            Action onFontChanged,
            IWin32Window ownerWindow)
        {
            NetService = netService;
            OnCheckUpdate = onCheckUpdate;
            OnShowChangeNick = onShowChangeNick;
            OnOpenFolder = onOpenFolder;
            OnFontChanged = onFontChanged;
            OwnerWindow = ownerWindow;
        }
    }
}
