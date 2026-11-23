document.addEventListener('click', event => {
    if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    const link = event.target instanceof Element ? event.target.closest('a[data-receipt-viewer]') : null;
    if (!link || typeof HTMLDialogElement === 'undefined') return;
    event.preventDefault();
    const previous = document.activeElement;
    const dialog = document.createElement('dialog');
    dialog.className = 'native-receipt-dialog';
    dialog.setAttribute('aria-label', 'Boletas de entrega');
    const header = document.createElement('header');
    const title = document.createElement('strong');
    title.textContent = link.dataset.receiptTitle || 'Boletas de entrega';
    const close = document.createElement('button');
    close.type = 'button'; close.textContent = 'Cerrar';
    close.addEventListener('click', () => dialog.close());
    header.append(title, close);
    const frame = document.createElement('iframe');
    frame.title = title.textContent;
    frame.src = link.href;
    const direct = document.createElement('a');
    direct.href = link.href; direct.target = '_blank'; direct.rel = 'noopener noreferrer';
    direct.textContent = 'Abrir boletas en otra pestaña';
    dialog.append(header, frame, direct);
    dialog.addEventListener('close', () => { dialog.remove(); if (previous?.isConnected) previous.focus(); }, { once: true });
    document.body.append(dialog);
    try { dialog.showModal(); close.focus(); }
    catch { dialog.remove(); window.open(link.href, '_blank', 'noopener'); }
});
