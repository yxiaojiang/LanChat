// 设置八页视图构建器与基础暗色控件工厂（分组标题、圆角卡片、下拉药丸、开关、刻度滑块）。
(function (root, factory) {
  const api = factory(root);
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatSettingsPages = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function (root) {
  const model = root.LanChatSettingsModel || (typeof require === 'function' ? require('./settings-model.js') : null);
  const appearance = root.LanChatAppearance || (typeof require === 'function' ? require('./appearance.js') : null);

  function createSection(title, spaced = false) {
    const heading = document.createElement('h2');
    heading.className = spaced ? 'settings-section-title spaced' : 'settings-section-title';
    heading.textContent = title;
    return heading;
  }

  function createCard() {
    const card = document.createElement('div');
    card.className = 'settings-card';
    return card;
  }

  function createRow(title, description, controls = [], extraClass = '') {
    const row = document.createElement('div');
    row.className = extraClass ? `settings-row ${extraClass}` : 'settings-row';
    const copy = document.createElement('div');
    copy.className = 'settings-row-copy';
    const titleEl = document.createElement('div');
    titleEl.className = 'settings-row-title';
    titleEl.textContent = title;
    copy.append(titleEl);
    let descEl = null;
    if (description !== null && description !== undefined) {
      descEl = document.createElement('div');
      descEl.className = 'settings-row-desc';
      descEl.textContent = description;
      copy.append(descEl);
    }
    const right = document.createElement('div');
    right.className = 'settings-row-controls';
    for (const control of controls) right.append(control);
    row.append(copy, right);
    return { row, titleEl, descEl };
  }

  function createDropdownPill(initialLabel, getItems, openMenu, leadingNode = null, labelRefList = null) {
    const pill = document.createElement('button');
    pill.type = 'button';
    pill.className = 'pill-dropdown';
    if (leadingNode) pill.append(leadingNode);
    const labelSpan = document.createElement('span');
    labelSpan.textContent = initialLabel;
    if (labelRefList) labelRefList.push(labelSpan);
    const chevron = document.createElement('span');
    chevron.className = 'pill-chevron';
    chevron.setAttribute('aria-hidden', 'true');
    pill.append(labelSpan, chevron);
    if (getItems) {
      pill.addEventListener('click', () => {
        openMenu(pill, getItems(labelSpan));
      });
    }
    return pill;
  }

  function createActionLink(label, onClick, danger = false) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = danger ? 'action-link danger' : 'action-link';
    btn.textContent = label;
    btn.addEventListener('click', () => onClick(btn));
    return btn;
  }

  function createToggleSwitch(checked, onToggle, ariaLabel) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'toggle-switch';
    btn.setAttribute('role', 'switch');
    btn.setAttribute('aria-checked', String(Boolean(checked)));
    btn.setAttribute('aria-label', ariaLabel);
    const knob = document.createElement('span');
    knob.className = 'toggle-knob';
    btn.append(knob);
    btn.addEventListener('click', () => {
      const next = btn.getAttribute('aria-checked') !== 'true';
      btn.setAttribute('aria-checked', String(next));
      onToggle(next, btn);
    });
    return btn;
  }

  // 自绘 5 档刻度滑块，支持点击、拖拽与左右方向键调节。
  function createSteppedSlider(values, currentValue, onChange, ariaLabel) {
    const labels = model.SIZE_LABELS;
    let index = model.sliderIndexForValue(values, currentValue);
    const rootEl = document.createElement('div');
    rootEl.className = 'stepped-slider';
    rootEl.tabIndex = 0;
    rootEl.setAttribute('role', 'slider');
    rootEl.setAttribute('aria-label', ariaLabel);
    rootEl.setAttribute('aria-valuemin', '0');
    rootEl.setAttribute('aria-valuemax', String(values.length - 1));

    const rail = document.createElement('div');
    rail.className = 'slider-rail';
    const ticks = document.createElement('div');
    ticks.className = 'slider-ticks';
    const labelEls = labels.map(text => {
      const step = document.createElement('span');
      step.className = 'slider-step';
      const tick = document.createElement('span');
      tick.className = 'slider-tick';
      const lbl = document.createElement('span');
      lbl.className = 'slider-label';
      lbl.textContent = text;
      step.append(tick, lbl);
      ticks.append(step);
      return lbl;
    });
    const knob = document.createElement('div');
    knob.className = 'slider-knob';
    rootEl.append(rail, ticks, knob);

    const render = () => {
      const pct = values.length > 1 ? (index / (values.length - 1)) * 100 : 0;
      knob.style.left = `calc(18px + (100% - 36px) * ${pct / 100})`;
      rootEl.setAttribute('aria-valuenow', String(index));
      rootEl.setAttribute('aria-valuetext', labels[index]);
      labelEls.forEach((el, idx) => el.classList.toggle('active', idx === index));
    };

    const pickByClientX = clientX => {
      const rect = rootEl.getBoundingClientRect();
      const pad = 18;
      const trackW = Math.max(1, rect.width - pad * 2);
      const rel = Math.max(0, Math.min(1, (clientX - rect.left - pad) / trackW));
      const next = Math.round(rel * (values.length - 1));
      if (next !== index) {
        index = next;
        render();
        onChange(values[index]);
      }
    };

    rootEl.addEventListener('pointerdown', event => {
      if (event.button !== 0) return;
      rootEl.focus();
      rootEl.setPointerCapture(event.pointerId);
      pickByClientX(event.clientX);
      const move = e => pickByClientX(e.clientX);
      const up = () => {
        rootEl.removeEventListener('pointermove', move);
        rootEl.removeEventListener('pointerup', up);
        rootEl.removeEventListener('pointercancel', up);
      };
      rootEl.addEventListener('pointermove', move);
      rootEl.addEventListener('pointerup', up);
      rootEl.addEventListener('pointercancel', up);
    });

    rootEl.addEventListener('keydown', event => {
      let next = index;
      if (event.key === 'ArrowLeft') next = Math.max(0, index - 1);
      else if (event.key === 'ArrowRight') next = Math.min(values.length - 1, index + 1);
      else if (event.key === 'Home') next = 0;
      else if (event.key === 'End') next = values.length - 1;
      else return;
      event.preventDefault();
      if (next !== index) {
        index = next;
        render();
        onChange(values[index]);
      }
    });

    render();
    return {
      el: rootEl,
      setIndex(next) {
        if (next >= 0 && next < values.length) {
          index = next;
          render();
        }
      }
    };
  }

  function buildAll(ctx) {
    const { committed, runtime, display, syncRefs, actions, activeTab, hasBridge } = ctx;

    function buildGeneralPage(page) {
      page.append(createSection('常规'));
      const card1 = createCard();
      card1.append(
        createRow('语言', null, [
          createDropdownPill(display.languagePill, labelEl => model.LANGUAGES.map(item => ({
            label: item.label,
            checked: display.languagePill === item.pill,
            run: () => { display.languagePill = item.pill; labelEl.textContent = item.pill; }
          })), actions.openMenu)
        ]).row,
        createRow('链接打开方式', '设置 LanChat 对话中链接的默认打开策略。', [
          createDropdownPill(display.linkPill, labelEl => model.LINK_MODES.map(item => ({
            label: item.label,
            checked: display.linkPill === item.pill,
            run: () => { display.linkPill = item.pill; labelEl.textContent = item.pill; }
          })), actions.openMenu)
        ]).row
      );
      const uiSlider = createSteppedSlider(model.UI_FONT_SIZES, committed.uiFontSize, value => {
        actions.updateAppearance({ uiFontSize: value });
      }, '字体大小');
      syncRefs.uiSliders.push(uiSlider);
      card1.append(createRow('字体大小', null, [uiSlider.el]).row);

      const dirRow = createRow('默认文件接收目录', model.formatShortPath(committed.downloadDirectory), [
        createActionLink('更改 >', () => actions.pickDirectory('downloads')),
        createActionLink('打开文件夹 >', actions.openDownloads)
      ]);
      syncRefs.downloadDescs.push(dirRow.descEl);
      card1.append(dirRow.row);
      page.append(card1);

      page.append(createSection('权限与偏好', true));
      const card2 = createCard();
      const autoSwitch = createToggleSwitch(committed.autoAcceptFiles, actions.toggleAutoAccept, '自动接收好友文件');
      syncRefs.autoSwitches.push(autoSwitch);
      card2.append(
        createRow('托盘未读悬浮预览', '新消息闪烁时鼠标悬停在托盘图标上显示发送人与摘要浮窗。', [
          createToggleSwitch(display.trayPreview, () => {}, '托盘未读悬浮预览')
        ]).row,
        createRow('自动接收好友文件', '局域网好友发送文件时自动保存到默认目录，无需手动逐一确认。', [autoSwitch]).row,
        createRow('后台网络驻留运行', '关闭主窗口后保持在系统托盘静默运行，随时响应局域网呼叫。', [
          createToggleSwitch(display.keepAlive, () => {}, '后台网络驻留运行')
        ]).row,
        createRow('网络协议通道', '物理网卡直连通信，多节点广播穿透加速。', [
          createDropdownPill('直接连接 (LAN)', null, actions.openMenu)
        ]).row
      );
      page.append(card2);

      page.append(createSection('支持与关于', true));
      const card3 = createCard();
      card3.append(
        createRow('关于 LanChat', `版本 v${runtime.version} · 局域网极速通讯与协同传输`, [
          createActionLink('检查更新 >', actions.showUpdateNotice)
        ]).row,
        createRow('意见与问题反馈', '帮助我们将 LanChat 打磨得更贴合你的直觉与习惯。', [
          createActionLink('发送反馈 >', actions.openFeedbackDialog)
        ]).row
      );
      page.append(card3);
    }

    function buildProfilePage(page) {
      page.append(createSection('个人名片'));
      const cardUser = createCard();
      const row = document.createElement('div');
      row.className = 'settings-row profile-card-row';
      const identity = document.createElement('div');
      identity.className = 'profile-identity';
      const avatar = document.createElement('img');
      avatar.className = 'profile-avatar';
      avatar.src = 'assets/MascotProfile.png';
      avatar.alt = '';
      const copy = document.createElement('div');
      copy.className = 'settings-row-copy';
      const nameEl = document.createElement('div');
      nameEl.className = 'profile-name';
      nameEl.textContent = committed.nickname || 'LanChat';
      syncRefs.nickLabels.push(nameEl);
      const subEl = document.createElement('div');
      subEl.className = 'settings-row-desc';
      subEl.textContent = `主机: ${runtime.hostname} · 局域网在线`;
      copy.append(nameEl, subEl);
      identity.append(avatar, copy);
      const controls = document.createElement('div');
      controls.className = 'settings-row-controls';
      controls.append(createActionLink('修改昵称 >', actions.openNickDialog));
      row.append(identity, controls);
      cardUser.append(row);
      page.append(cardUser);

      page.append(createSection('设备标识', true));
      const cardDev = createCard();
      cardDev.append(
        createRow('节点识别码 (Node UUID)', runtime.id, [
          createActionLink('复制 ID >', async btn => {
            if (await actions.copyText(runtime.id)) btn.textContent = '已复制 ✓';
          })
        ]).row,
        createRow('操作系统环境', runtime.osText, []).row
      );
      page.append(cardDev);
    }

    function buildAppearancePage(page) {
      page.append(createSection('外观偏好'));
      const cardPalette = createCard();
      const themeIdx = appearance.normalizeThemeMode(committed.themeMode);
      const accentIdx = appearance.normalizeAccentPreset(committed.accentPreset);
      cardPalette.append(
        createRow('主题模式', null, [
          createDropdownPill(model.THEMES[themeIdx], () => model.THEMES.map((name, idx) => ({
            label: name,
            checked: appearance.normalizeThemeMode(committed.themeMode) === idx,
            run: () => { actions.updateAppearance({ themeMode: idx }); }
          })), actions.openMenu, null, syncRefs.themeLabels)
        ]).row,
        createRow('强调色', null, [
          createDropdownPill(model.ACCENTS[accentIdx].label, () => model.ACCENTS.map((item, idx) => ({
            label: item.label,
            swatch: item.swatch,
            checked: appearance.normalizeAccentPreset(committed.accentPreset) === idx,
            run: () => { actions.updateAppearance({ accentPreset: idx }); }
          })), actions.openMenu, null, syncRefs.accentLabels)
        ]).row
      );
      page.append(cardPalette);

      page.append(createSection('字体与排版', true));
      const cardFonts = createCard();
      const uiSlider = createSteppedSlider(model.UI_FONT_SIZES, committed.uiFontSize, value => {
        actions.updateAppearance({ uiFontSize: value });
      }, '主界面字体大小');
      syncRefs.uiSliders.push(uiSlider);
      const chatSlider = createSteppedSlider(model.CHAT_FONT_SIZES, committed.chatFontSize, value => {
        actions.updateAppearance({ chatFontSize: value });
      }, '聊天框字体大小');
      syncRefs.chatSliders.push(chatSlider);

      cardFonts.append(
        createRow('主界面字体样式', null, [
          createDropdownPill(appearance.fontDisplayLabel(committed.uiFontFamily), () => model.UI_FONT_PRESETS.map(item => ({
            label: item.label,
            checked: appearance.normalizeFontFamily(committed.uiFontFamily) === item.family,
            run: () => { actions.updateAppearance({ uiFontFamily: item.family }); }
          })), actions.openMenu, null, syncRefs.uiFontLabels)
        ]).row,
        createRow('主界面字体大小', '缩放会话列表、标题、搜索和输入文字。', [uiSlider.el]).row,
        createRow('聊天框字体样式', null, [
          createDropdownPill(appearance.fontDisplayLabel(committed.chatFontFamily), () => model.CHAT_FONT_PRESETS.map(item => ({
            label: item.label,
            checked: appearance.normalizeFontFamily(committed.chatFontFamily) === item.family,
            run: () => { actions.updateAppearance({ chatFontFamily: item.family }); }
          })), actions.openMenu, null, syncRefs.chatFontLabels)
        ]).row,
        createRow('聊天框字体大小', '调整聊天消息气泡正文排版字号。', [chatSlider.el]).row
      );
      page.append(cardFonts);
    }

    function buildShortcutsPage(page) {
      page.append(createSection('快捷操作'));
      const card = createCard();
      for (const item of model.SHORTCUTS) {
        const badge = document.createElement('span');
        badge.className = 'shortcut-badge';
        badge.textContent = item.keys;
        card.append(createRow(item.title, null, [badge]).row);
      }
      page.append(card);
    }

    function buildFileTransferPage(page) {
      page.append(createSection('文件偏好'));
      const card = createCard();
      const autoSwitch = createToggleSwitch(committed.autoAcceptFiles, actions.toggleAutoAccept, '自动接收好友文件');
      syncRefs.autoSwitches.push(autoSwitch);
      const dirRow = createRow('接收文件默认存储目录', model.formatShortPath(committed.downloadDirectory), [
        createActionLink('更改 >', () => actions.pickDirectory('downloads')),
        createActionLink('打开文件夹 >', actions.openDownloads)
      ]);
      syncRefs.downloadDescs.push(dirRow.descEl);

      card.append(
        createRow('自动接收好友文件', '局域网内接收文件时无需手动逐一确认保存。', [autoSwitch]).row,
        dirRow.row,
        createRow('64KB 分块极速传输校验', '纯原生 LANC 协议流式流转，零中转零依赖。', [
          createToggleSwitch(display.chunkCheck, () => {}, '64KB 分块极速传输校验')
        ]).row
      );
      page.append(card);
    }

    function buildHistoryPage(page) {
      page.append(createSection('聊天记录管理'));
      const card = createCard();
      const histRow = createRow('聊天记录存储目录', model.formatShortPath(committed.historyDirectory), [
        createActionLink('更改 >', () => actions.pickDirectory('history')),
        createActionLink('打开文件夹 >', actions.openHistory)
      ]);
      syncRefs.historyDescs.push(histRow.descEl);

      card.append(
        histRow.row,
        createRow('自动加载历史消息', '重启或更新覆盖安装后，自动读取历史会话与消息流。', [
          createToggleSwitch(display.autoLoadHistory, () => {}, '自动加载历史消息')
        ]).row,
        createRow('彻底清空所有本地聊天记录', '彻底清除本机保存的所有广播与好友聊天历史文件。', [
          createActionLink('立即清空 >', btn => actions.clearAllHistories(btn), true)
        ]).row
      );
      page.append(card);
    }

    function buildDevicePage(page) {
      page.append(createSection('物理网络与设备'));
      const card = createCard();
      const dot = document.createElement('span');
      dot.className = runtime.connected || !hasBridge ? 'status-dot' : 'status-dot offline';
      const statusLabel = runtime.connected || !hasBridge ? '局域网在线' : '服务已断开';
      const statusDesc = `IP: ${runtime.ip}:${runtime.tcpPort}  ·  UDP 38888 广播服务就绪`;
      card.append(
        createRow('物理以太网 / Wi-Fi IP 绑定', statusDesc, [
          createDropdownPill(statusLabel, null, actions.openMenu, dot)
        ]).row,
        createRow('TUN / TAP 虚拟网卡穿透过滤', '智能识别并过滤 Clash / WireGuard 虚拟网卡，防止广播风暴。', [
          createToggleSwitch(display.tunFilter, () => {}, 'TUN / TAP 虚拟网卡穿透过滤')
        ]).row,
        createRow('多网卡并发单播探测', '在局域网复杂交换机拓扑下并发向物理子网各节点单播探测。', [
          createToggleSwitch(display.unicastProbe, () => {}, '多网卡并发单播探测')
        ]).row
      );
      page.append(card);
    }

    function buildAboutPage(page) {
      page.append(createSection('关于产品'));
      const cardAbout = createCard();
      const row = document.createElement('div');
      row.className = 'settings-row about-hero-row';
      const brand = document.createElement('div');
      brand.className = 'about-brand';
      const logoBox = document.createElement('div');
      logoBox.className = 'about-logo-box';
      const logoImg = document.createElement('img');
      logoImg.src = 'assets/MascotWhite.png';
      logoImg.alt = '';
      logoBox.append(logoImg);

      const meta = document.createElement('div');
      const title = document.createElement('div');
      title.className = 'about-meta-title';
      title.textContent = 'LanChat 局域网极速通讯';
      const line2 = document.createElement('div');
      line2.className = 'about-meta-line';
      line2.textContent = `版本 v${runtime.version} · WorkBuddy 1:1 对标现代化极简暗黑视效`;
      const line3 = document.createElement('div');
      line3.className = 'about-meta-sub';
      line3.textContent = '原生高性能零冗余依赖架构 · Copyright © 2026 LanChat Team';
      meta.append(title, line2, line3);
      brand.append(logoBox, meta);

      const actionsCol = document.createElement('div');
      actionsCol.className = 'about-actions-col';
      actionsCol.append(
        createActionLink('检查更新 >', actions.showUpdateNotice),
        createActionLink('复制诊断信息 >', async btn => {
          const text = model.buildDiagnostics({
            version: runtime.version,
            osText: runtime.osText,
            machine: runtime.hostname,
            arch: runtime.arch,
            electronVersion: runtime.electronVersion,
            address: `${runtime.ip}:${runtime.tcpPort}`
          });
          if (await actions.copyText(text)) btn.textContent = '已复制 ✓';
        })
      );
      row.append(brand, actionsCol);
      cardAbout.append(row);
      page.append(cardAbout);

      page.append(createSection('支持与反馈', true));
      const cardFk = createCard();
      cardFk.append(
        createRow('意见与问题反馈', '帮助我们将 LanChat 打磨得更贴合你的直觉与习惯。', [
          createActionLink('发送反馈 >', actions.openFeedbackDialog)
        ], 'feedback-card-row').row
      );
      page.append(cardFk);
    }

    const builders = [
      buildGeneralPage, buildProfilePage, buildAppearancePage, buildShortcutsPage,
      buildFileTransferPage, buildHistoryPage, buildDevicePage, buildAboutPage
    ];
    return builders.map((build, id) => {
      const page = document.createElement('section');
      page.className = 'settings-page';
      page.dataset.tabId = String(id);
      page.hidden = id !== activeTab;
      build(page);
      return page;
    });
  }

  return { buildAll };
});
