// 应用内暗色菜单、确认框和表情面板。焦点离开后回到打开它们的控件。
(function (root, factory) {
  const api = factory(root);
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatOverlay = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function (root) {
  const presentation = root.LanChatPresentation || (typeof require === 'function' ? require('./presentation.js') : null);
  const menu = document.getElementById('menu');
  const emoji = document.getElementById('emoji');
  const emojiGrid = document.getElementById('emoji-grid');
  const confirmDialog = document.getElementById('confirm');
  let restore = null;
  let restoreKey = '';
  let menuTrigger = null;
  let emojiTrigger = null;
  let suppressAttachToggle = false;
  let attachResetTimer = 0;
  let releaseArmed = false;
  let pickEmoji = null;
  const emojiScroll = root.LanChatScroll?.bind(emojiGrid, { keyboard: false });

  function clearAttachToggle() {
    if (attachResetTimer) {
      clearTimeout(attachResetTimer);
      attachResetTimer = 0;
    }
    suppressAttachToggle = false;
  }

  function disarmAttachRelease() {
    if (!releaseArmed) return;
    releaseArmed = false;
    document.removeEventListener('pointerup', onAttachRelease);
    document.removeEventListener('pointercancel', onAttachRelease);
    window.removeEventListener('blur', clearAttachToggle);
  }

  function onAttachRelease() {
    disarmAttachRelease();
    if (attachResetTimer) clearTimeout(attachResetTimer);
    attachResetTimer = setTimeout(() => {
      attachResetTimer = 0;
      suppressAttachToggle = false;
    }, 0);
  }

  function armAttachToggle() {
    if (attachResetTimer) {
      clearTimeout(attachResetTimer);
      attachResetTimer = 0;
    }
    suppressAttachToggle = true;
    if (releaseArmed) return;
    releaseArmed = true;
    document.addEventListener('pointerup', onAttachRelease);
    document.addEventListener('pointercancel', onAttachRelease);
    window.addEventListener('blur', clearAttachToggle);
  }

  function focusTarget(target) {
    if (target && target.isConnected) target.focus();
  }

  function sessionButton(key) {
    if (!key) return null;
    return [...document.querySelectorAll('#chats .chat')].find(button => button.dataset.key === key) || null;
  }

  function closeMenu(shouldRestore = true) {
    if (menu.hidden) return;
    menu.hidden = true;
    menu.replaceChildren();
    const target = sessionButton(restoreKey) || restore;
    restore = null;
    restoreKey = '';
    menuTrigger = null;
    if (shouldRestore) focusTarget(target);
  }

  function closeEmoji(shouldRestore = true) {
    if (emoji.hidden) return;
    emoji.hidden = true;
    pickEmoji = null;
    const target = restore;
    restore = null;
    emojiTrigger = null;
    if (shouldRestore) focusTarget(target);
  }

  function place(element, point) {
    element.hidden = false;
    const box = element.getBoundingClientRect();
    const left = Math.max(8, Math.min(point.x, window.innerWidth - box.width - 8));
    const preferredTop = point.above ? point.y - box.height : point.y;
    const top = Math.max(8, Math.min(preferredTop, window.innerHeight - box.height - 8));
    element.style.left = `${left}px`;
    element.style.top = `${top}px`;
  }

  function openMenu(items, point, focusBack) {
    clearAttachToggle();
    disarmAttachRelease();
    closeEmoji(false);
    restore = focusBack || document.activeElement;
    restoreKey = focusBack?.dataset?.key || '';
    menuTrigger = focusBack || null;
    menu.replaceChildren();
    const buttons = [];
    for (const item of items) {
      if (item.separator) {
        menu.append(document.createElement('hr'));
        continue;
      }
      const button = document.createElement('button');
      button.type = 'button';
      button.className = item.danger ? 'danger' : '';
      button.setAttribute('role', 'menuitem');
      if (item.icon) {
        const icon = document.createElement('img');
        icon.src = `assets/${item.icon}.png`;
        icon.alt = '';
        button.append(icon);
      }
      const label = document.createElement('span');
      label.textContent = item.label;
      button.append(label);
      if (item.hint) {
        const hint = document.createElement('span');
        hint.className = 'menu-hint';
        hint.textContent = item.hint;
        button.append(hint);
      }
      button.disabled = Boolean(item.disabled);
      button.addEventListener('click', () => {
        const run = item.run;
        closeMenu(!item.keepSelection);
        run();
      });
      menu.append(button);
      if (!button.disabled) buttons.push(button);
    }
    place(menu, point);
    buttons[0]?.focus();
  }

  function openEmoji(anchor, onPick, focusBack) {
    clearAttachToggle();
    disarmAttachRelease();
    closeMenu(false);
    restore = focusBack || anchor;
    emojiTrigger = anchor || null;
    pickEmoji = onPick;
    drawEmojiGrid();
    emoji.hidden = false;
    const rect = anchor.getBoundingClientRect();
    const box = emoji.getBoundingClientRect();
    place(emoji, {
      x: rect.left,
      y: rect.top - box.height - 6
    });
    emojiScroll?.refresh();
    emoji.querySelector('button')?.focus();
  }

  function emojiColumns(buttons) {
    if (buttons.length < 2) return 1;
    const firstTop = buttons[0].offsetTop;
    const nextRow = buttons.findIndex(item => item.offsetTop > firstTop);
    return nextRow > 0 ? nextRow : buttons.length;
  }

  emoji.addEventListener('keydown', event => {
    if (event.key === 'Escape') {
      event.preventDefault();
      closeEmoji();
      return;
    }
    const buttons = [...emoji.querySelectorAll('button')];
    if (!buttons.length) return;
    const index = buttons.indexOf(document.activeElement);
    const cols = emojiColumns(buttons);
    let next = -1;
    if (event.key === 'Tab') {
      next = event.shiftKey ? (index <= 0 ? buttons.length - 1 : index - 1) : (index < 0 ? 0 : (index + 1) % buttons.length);
    } else if (event.key === 'ArrowRight') next = index < 0 ? 0 : (index + 1) % buttons.length;
    else if (event.key === 'ArrowLeft') next = index <= 0 ? buttons.length - 1 : index - 1;
    else if (event.key === 'ArrowDown') next = index < 0 ? 0 : Math.min(buttons.length - 1, index + cols);
    else if (event.key === 'ArrowUp') next = index < 0 ? 0 : Math.max(0, index - cols);
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = buttons.length - 1;
    if (next >= 0) {
      event.preventDefault();
      buttons[next]?.focus();
    }
  });

  menu.addEventListener('keydown', event => {
    const buttons = [...menu.querySelectorAll('button:not(:disabled)')];
    if (event.key === 'Escape') {
      event.preventDefault();
      closeMenu();
      return;
    }
    if (!buttons.length) return;
    const index = buttons.indexOf(document.activeElement);
    const forward = event.key === 'ArrowDown' || (event.key === 'Tab' && !event.shiftKey);
    const backward = event.key === 'ArrowUp' || (event.key === 'Tab' && event.shiftKey);
    if (forward) {
      event.preventDefault();
      buttons[(index < 0 ? 0 : index + 1) % buttons.length]?.focus();
    } else if (backward) {
      event.preventDefault();
      buttons[(index <= 0 ? buttons.length : index) - 1]?.focus();
    } else if (event.key === 'Home') {
      event.preventDefault();
      buttons[0]?.focus();
    } else if (event.key === 'End') {
      event.preventDefault();
      buttons.at(-1)?.focus();
    }
  });

  document.addEventListener('keydown', event => {
    if (event.key !== 'Escape') return;
    if (!menu.hidden) {
      event.preventDefault();
      closeMenu();
    } else if (!emoji.hidden) {
      event.preventDefault();
      closeEmoji();
    }
  });
  document.addEventListener('pointerdown', event => {
    const attachBtn = document.getElementById('attach');
    const onAttach = Boolean(attachBtn && attachBtn.contains(event.target));
    if (!menu.hidden && !menu.contains(event.target)) {
      if (onAttach && menuTrigger === attachBtn) armAttachToggle();
      closeMenu();
    }
    if (!emoji.hidden && !emoji.contains(event.target)) {
      if (onAttach && emojiTrigger === attachBtn) armAttachToggle();
      closeEmoji();
    }
  });
  window.addEventListener('resize', () => {
    closeMenu();
    closeEmoji();
  });

  function drawEmojiGrid() {
    emojiGrid.replaceChildren();
    for (const [symbol, imageUrl] of presentation.emojiEntries()) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'emoji';
      button.title = symbol;
      button.setAttribute('aria-label', symbol);
      if (imageUrl) {
        const image = document.createElement('img');
        image.src = imageUrl;
        image.alt = '';
        button.append(image);
      } else {
        button.textContent = symbol;
      }
      button.addEventListener('click', () => {
        const choose = pickEmoji;
        closeEmoji();
        if (choose) choose(symbol);
      });
      emojiGrid.append(button);
    }
  }

  function consumeAttachToggle() {
    const active = suppressAttachToggle;
    clearAttachToggle();
    disarmAttachRelease();
    return active;
  }

  function confirmAction({ title, message, confirmLabel }) {
    if (confirmDialog.open) return Promise.resolve(false);
    const previousFocus = document.activeElement;
    document.getElementById('confirm-title').textContent = title;
    document.getElementById('confirm-body').textContent = message;
    document.getElementById('confirm-accept').textContent = confirmLabel;
    confirmDialog.returnValue = 'cancel';
    confirmDialog.showModal();
    document.getElementById('confirm-cancel').focus();
    return new Promise(resolve => {
      confirmDialog.addEventListener('close', () => {
        focusTarget(previousFocus);
        resolve(confirmDialog.returnValue === 'ok');
      }, { once: true });
    });
  }

  return { openMenu, closeMenu, openEmoji, closeEmoji, consumeAttachToggle, confirmAction };
});
