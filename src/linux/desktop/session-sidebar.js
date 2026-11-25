(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatSessionSidebar = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function () {
  // 会话侧栏与主窗同生命周期。依赖展示规则与菜单，通过回调切换会话和重绘。
  function createSessionSidebar({ doc, presentation, overlay, switchChat, menuFor, redraw }) {
    const nav = doc.getElementById('chats');
    const search = doc.getElementById('search');
    const searchClear = doc.getElementById('search-clear');
    const group = presentation.GROUP;

    function sessionButton(id) {
      return [...nav.querySelectorAll('.chat')].find(item => item.dataset.key === id);
    }

    function focusSession(id) {
      const button = sessionButton(id);
      button?.focus();
    }

    function appendSessionHeading(online) {
      const heading = doc.createElement('h2');
      heading.className = 'session-heading';
      heading.dataset.online = String(online);
      heading.textContent = online ? '在线' : '离线';
      nav.append(heading);
    }

    function render({ peers, messages, unread, active }) {
      const restoreKey = doc.activeElement?.classList?.contains('chat') ? doc.activeElement.dataset.key : '';
      const previousScrollTop = nav.scrollTop;
      nav.replaceChildren();
      const filter = search.value;
      const rows = presentation.sessionRows({ peers, messages, filter });
      let onlineHeading = false;
      let offlineHeading = false;
      for (const entry of rows) {
        if (!entry.pinned && entry.online && !onlineHeading) {
          appendSessionHeading(true);
          onlineHeading = true;
        }
        if (!entry.pinned && !entry.online && !offlineHeading) {
          if (!onlineHeading && !filter.trim()) {
            appendSessionHeading(true);
            onlineHeading = true;
          }
          appendSessionHeading(false);
          offlineHeading = true;
        }
        const button = doc.createElement('button');
        button.type = 'button';
        button.className = entry.pinned ? 'chat pinned' : 'chat';
        button.dataset.key = entry.key;
        button.setAttribute('aria-current', String(entry.key === active));
        const count = presentation.unreadText(unread.get(entry.key));
        button.setAttribute('aria-label', [entry.fullName, entry.pinned ? '置顶' : '', count ? `${count} 条未读` : ''].filter(Boolean).join('，'));
        const card = doc.createElement('span');
        card.className = 'chat-card';
        const avatarWrap = doc.createElement('span');
        avatarWrap.className = 'avatar-wrap';
        const avatar = doc.createElement('img');
        avatar.src = entry.key === group ? 'assets/group.png' : 'assets/peer.png';
        avatar.alt = '';
        avatarWrap.append(avatar);
        if (count) {
          const badge = doc.createElement('span');
          badge.className = 'badge';
          badge.textContent = count;
          avatarWrap.append(badge);
        }
        const labels = doc.createElement('span');
        labels.className = 'chat-copy';
        const titleRow = doc.createElement('span');
        titleRow.className = 'title-row';
        const title = doc.createElement('strong');
        title.textContent = entry.title;
        titleRow.append(title);
        if (entry.pinned) {
          const pin = doc.createElement('em');
          pin.className = 'pin';
          pin.textContent = '置顶';
          titleRow.append(pin);
        }
        const summary = doc.createElement('small');
        presentation.drawMessageText(summary, entry.snippet, doc, true);
        labels.append(titleRow, summary);
        card.append(avatarWrap, labels);
        button.append(card);
        button.addEventListener('click', () => switchChat(entry.key, true));
        button.addEventListener('contextmenu', event => {
          event.preventDefault();
          const point = { x: event.clientX, y: event.clientY };
          switchChat(entry.key, false);
          overlay.openMenu(menuFor(entry), point, sessionButton(entry.key));
        });
        nav.append(button);
      }
      if (!filter.trim() && !offlineHeading) {
        if (!onlineHeading) appendSessionHeading(true);
        appendSessionHeading(false);
      }
      nav.scrollTop = previousScrollTop;
      if (restoreKey) focusSession(restoreKey);
    }

    search.addEventListener('input', () => {
      overlay.closeMenu();
      searchClear.hidden = !search.value;
      redraw();
      search.focus();
    });
    search.addEventListener('keydown', event => {
      if (event.key !== 'Escape' || !search.value) return;
      event.preventDefault();
      search.value = '';
      searchClear.hidden = true;
      redraw();
      search.focus();
    });
    searchClear.addEventListener('click', () => {
      search.value = '';
      searchClear.hidden = true;
      redraw();
      search.focus();
    });
    nav.addEventListener('keydown', event => {
      if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
      const rows = [...nav.querySelectorAll('.chat')];
      if (!rows.length) return;
      const index = rows.findIndex(button => button.getAttribute('aria-current') === 'true');
      const next = index < 0
        ? (event.key === 'ArrowDown' ? rows[0] : rows.at(-1))
        : rows[index + (event.key === 'ArrowDown' ? 1 : -1)];
      if (!next) return;
      event.preventDefault();
      switchChat(next.dataset.key, false);
    });
    nav.addEventListener('scroll', () => overlay.closeMenu());

    return { render, focusSession };
  }

  return { createSessionSidebar };
});
