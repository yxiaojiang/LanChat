// 会话、聊天和传输列表共用的暗色浮动滑块。静止 4px，悬停或拖拽 5px，没有溢出时隐藏。
(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.LanChatScroll = api;
})(typeof globalThis === 'undefined' ? this : globalThis, function () {
  function bind(view, options = {}) {
    const host = view.parentElement;
    const thumb = document.createElement('div');
    thumb.className = 'float-thumb';
    thumb.hidden = true;
    thumb.setAttribute('role', 'scrollbar');
    thumb.setAttribute('aria-orientation', 'vertical');
    thumb.setAttribute('aria-controls', view.id);
    thumb.setAttribute('aria-valuemin', '0');
    host.append(thumb);

    let rafId = 0;
    const update = () => {
      rafId = 0;
      const overflow = view.scrollHeight - view.clientHeight;
      const hidden = host.hidden || view.hidden || overflow <= 1;
      thumb.hidden = hidden;
      if (hidden) return;
      const size = Math.max(24, view.clientHeight * view.clientHeight / view.scrollHeight);
      const travel = Math.max(1, view.clientHeight - size);
      thumb.style.height = `${size}px`;
      thumb.style.transform = `translateY(${view.scrollTop / overflow * travel}px)`;
      thumb.setAttribute('aria-valuenow', String(Math.round(view.scrollTop)));
      thumb.setAttribute('aria-valuemax', String(Math.round(overflow)));
    };
    const schedule = () => {
      if (rafId) return;
      rafId = typeof requestAnimationFrame === 'function' ? requestAnimationFrame(update) : (update(), 0);
    };

    const isEditable = view.tagName === 'TEXTAREA' || view.tagName === 'INPUT' || view.isContentEditable;
    view.addEventListener('scroll', update, { passive: true });
    if (isEditable) view.addEventListener('input', schedule);
    if (typeof ResizeObserver === 'function') new ResizeObserver(schedule).observe(view);
    if (typeof MutationObserver === 'function') {
      new MutationObserver(schedule).observe(view, { childList: true, subtree: true, characterData: true });
      new MutationObserver(schedule).observe(host, { attributes: true, attributeFilter: ['hidden'] });
    }
    thumb.addEventListener('pointerdown', event => {
      if (event.button !== 0 || thumb.hidden) return;
      event.preventDefault();
      thumb.setPointerCapture(event.pointerId);
      thumb.classList.add('dragging');
      const origin = event.clientY;
      const start = view.scrollTop;
      const overflow = view.scrollHeight - view.clientHeight;
      const travel = Math.max(1, view.clientHeight - thumb.getBoundingClientRect().height);
      const move = current => {
        view.scrollTop = start + (current.clientY - origin) / travel * overflow;
      };
      const end = () => {
        thumb.classList.remove('dragging');
        thumb.removeEventListener('pointermove', move);
        thumb.removeEventListener('pointerup', end);
        thumb.removeEventListener('pointercancel', end);
      };
      thumb.addEventListener('pointermove', move);
      thumb.addEventListener('pointerup', end);
      thumb.addEventListener('pointercancel', end);
    });
    if (!isEditable && options.keyboard !== false) {
      view.addEventListener('keydown', event => {
        if (event.target !== view || event.isComposing || event.keyCode === 229 ||
            event.shiftKey || event.altKey || event.ctrlKey || event.metaKey) return;
        const page = view.clientHeight * 0.9;
        if (event.key === 'Home') view.scrollTop = 0;
        else if (event.key === 'End') view.scrollTop = view.scrollHeight;
        else if (event.key === 'PageDown') view.scrollTop += page;
        else if (event.key === 'PageUp') view.scrollTop -= page;
        else if (event.key === 'ArrowDown') view.scrollTop += 48;
        else if (event.key === 'ArrowUp') view.scrollTop -= 48;
        else return;
        event.preventDefault();
      });
    }
    update();
    return { refresh: schedule, update };
  }

  return { bind };
});
