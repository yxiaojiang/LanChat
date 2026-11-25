(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatConversationView = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function () {
  // 聊天记录视图与主窗同生命周期，负责气泡、文件卡片、滚动和文本复制。主窗管理消息及传输状态。
  function createConversationView({ doc, win, feed, presentation, overlay, openPath, status }) {
    const group = presentation.GROUP;

    function formatTransferRate(value) {
      const speed = Number(value);
      if (!Number.isFinite(speed) || speed <= 0) return '';
      if (speed < 1) return `${Math.round(speed * 1024)} KB/s`;
      return `${speed < 10 ? speed.toFixed(2) : speed.toFixed(1)} MB/s`;
    }

    // 追加单条消息时保留正在浏览历史的视口位置，自己发送的消息始终滚到末尾。
    function appendMessage(message, previous) {
      const nearBottom = feed.scrollHeight - feed.scrollTop - feed.clientHeight < 32;
      appendRendered(message, previous, feed);
      if (nearBottom || message.self) feed.scrollTop = feed.scrollHeight;
    }

    function appendRendered(message, previous, target) {
      const view = presentation.decorate(previous, message, message.chat === group);
      if (view.showTime) {
        const time = doc.createElement('div');
        time.className = 'time-header';
        time.textContent = view.timeLabel;
        target.append(time);
      }
      const row = doc.createElement('div');
      row.className = message.system ? 'message-row system' : message.self ? 'message-row self' : 'message-row';
      if (message.system) {
        row.textContent = message.text || '';
        target.append(row);
        return;
      }
      if (view.showSender) {
        const sender = doc.createElement('div');
        sender.className = 'sender';
        sender.textContent = message.nick || '节点';
        row.append(sender);
      }
      if (message.file) row.append(fileCard(message));
      else {
        const bubble = doc.createElement('div');
        bubble.className = 'bubble';
        bubble.tabIndex = -1;
        presentation.drawMessageText(bubble, message.text || '', doc);
        row.append(bubble);
      }
      target.append(row);
    }

    function fileCard(message) {
      const card = doc.createElement('article');
      card.className = 'file-card';
      const badge = doc.createElement('span');
      badge.className = 'file-badge';
      badge.setAttribute('aria-hidden', 'true');
      const badgeIcon = doc.createElement('img');
      badgeIcon.src = 'assets/BoxWhite.png';
      badgeIcon.alt = '';
      badge.append(badgeIcon);
      const name = doc.createElement('strong');
      name.textContent = message.name || '未知文件';
      const size = doc.createElement('small');
      size.textContent = presentation.formatFileSize(message.size) + (message.exists === false ? ' · 原文件不可用' : '');
      card.append(badge, name, size);
      if (message.transferState) {
        const progress = doc.createElement('div');
        progress.className = 'file-progress';
        const bar = doc.createElement('progress');
        bar.max = 100;
        bar.setAttribute('aria-label', message.name || '文件传输进度');
        const label = doc.createElement('small');
        progress.append(bar, label);
        card.append(progress);
        message.card = card;
        message.progressView = { bar, label };
        paintProgress(message);
        return card;
      }
      const actions = doc.createElement('div');
      actions.className = 'file-actions';
      actions.append(fileAction('打开文件', message, 'file', message.exists === false), fileAction('打开目录', message, 'directory', false));
      card.append(actions);
      return card;
    }

    function paintProgress(message) {
      if (!message.progressView) return;
      const { bar, label } = message.progressView;
      const state = message.transferState;
      const action = state.sending ? '发送' : '接收';
      const percent = state.completed ? 100 : state.total > 0
        ? Math.min(100, Math.max(0, Math.floor(100 * (state.received ?? 0) / state.total))) : 0;
      bar.value = percent;
      label.classList.toggle('error', Boolean(state.failed || state.unknown));
      const rate = formatTransferRate(state.speed);
      const suffix = rate ? ` · ${rate}` : '';
      label.textContent = state.unknown ? (state.queued ? '未发送' : `${action}结果未确认 · ${percent}%`) :
        state.failed ? `${action}失败${state.error ? ` · ${state.error}` : ''}` :
        state.completed ? `${action}完成 · 100%` : `${state.queued ? '等待发送' : `${action}中`} · ${percent}%${suffix}`;
      label.title = label.textContent;
    }

    function replaceFileCard(previous, message) {
      if (feed.contains(previous.card)) previous.card.replaceWith(fileCard(message));
    }

    function fileAction(label, message, kind, unavailable) {
      const button = doc.createElement('button');
      button.type = 'button';
      button.textContent = label;
      button.disabled = unavailable || !message.path;
      button.addEventListener('click', async () => {
        try {
          await openPath(message.path, kind);
        } catch (error) {
          status(error.message, true);
        }
      });
      return button;
    }

    function render(messages, scrollToBottom) {
      const position = feed.scrollTop;
      const fragment = doc.createDocumentFragment();
      let previous = null;
      for (const item of messages) {
        appendRendered(item, previous, fragment);
        previous = item;
      }
      feed.replaceChildren(fragment);
      feed.scrollTop = scrollToBottom ? feed.scrollHeight : position;
    }

    async function copyText(text) {
      if (!text) return;
      try {
        const clipboard = win.navigator?.clipboard;
        if (clipboard?.writeText) {
          await clipboard.writeText(text);
          return;
        }
      } catch {}
      const helper = doc.createElement('textarea');
      helper.value = text;
      helper.style.position = 'fixed';
      helper.style.opacity = '0';
      doc.body.append(helper);
      helper.select();
      doc.execCommand('copy');
      helper.remove();
    }

    function selectBubbleText(bubble) {
      const selection = win.getSelection();
      if (!selection) return;
      const range = doc.createRange();
      range.selectNodeContents(bubble);
      selection.removeAllRanges();
      selection.addRange(range);
    }

    function bubbleSelectedText(bubble) {
      const selection = win.getSelection();
      if (!selection || selection.isCollapsed || !selection.rangeCount) return '';
      const range = selection.getRangeAt(0);
      return bubble.contains(range.commonAncestorContainer) ? selection.toString() : '';
    }

    feed.addEventListener('contextmenu', event => {
      const bubble = event.target.closest('.bubble');
      if (!bubble) return;
      event.preventDefault();
      const full = bubble.textContent || '';
      const selected = bubbleSelectedText(bubble);
      overlay.openMenu([
        { label: '复制', icon: 'Copy', hint: 'Ctrl+C', run: () => { void copyText(selected || full); } },
        { label: '复制全文', icon: 'Copy', run: () => { void copyText(full); } },
        { separator: true },
        { label: '全选', icon: 'User', hint: 'Ctrl+A', keepSelection: true, run: () => selectBubbleText(bubble) }
      ], { x: event.clientX, y: event.clientY }, bubble);
    });
    feed.addEventListener('keydown', event => {
      if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey || event.key.toLowerCase() !== 'a') return;
      const anchorNode = win.getSelection()?.anchorNode;
      const anchorEl = anchorNode?.nodeType === 1 ? anchorNode : anchorNode?.parentElement;
      const bubble = doc.activeElement?.closest?.('.bubble') || anchorEl?.closest?.('.bubble');
      if (!bubble || !feed.contains(bubble)) return;
      event.preventDefault();
      selectBubbleText(bubble);
    });

    return { appendMessage, render, replaceFileCard, paintProgress };
  }

  return { createConversationView };
});
