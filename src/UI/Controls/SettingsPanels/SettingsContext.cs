using System;
using System.Windows.Forms;

namespace LanChat
{
    /// <summary>
    /// 设置子面板轻量依赖注入上下文 DTO
    /// <para>【职责】解耦设置中心各大独立子面板与主窗体/设置窗体的直接强依赖，汇集网络服务与弹窗回调生命周期。</para>
    /// </summary>
    public class SettingsContext
    {
        /// <summary>网络通信底层服务实例引用</summary>
        public NetworkService NetService { get; set; }
        /// <summary>触发检查更新弹窗的回调函数</summary>
        public Action OnCheckUpdate { get; set; }
        /// <summary>触发修改昵称弹窗的回调函数</summary>
        public Action OnShowChangeNick { get; set; }
        /// <summary>触发打开接收文件夹的回调函数</summary>
        public Action OnOpenFolder { get; set; }
        /// <summary>字体变更后通知全局视口重算排版的回调函数</summary>
        public Action OnFontChanged { get; set; }
        /// <summary>宿主窗体引用（作为模态弹窗的父级 Owner 窗口）</summary>
        public IWin32Window OwnerWindow { get; set; }

        /// <summary>
        /// 初始化设置面板上下文参数集
        /// </summary>
        /// <param name="netService">网络通信服务</param>
        /// <param name="onCheckUpdate">检查更新回调</param>
        /// <param name="onShowChangeNick">修改昵称回调</param>
        /// <param name="onOpenFolder">打开文件夹回调</param>
        /// <param name="onFontChanged">全局字体刷新回调</param>
        /// <param name="ownerWindow">父级宿主窗体</param>
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
