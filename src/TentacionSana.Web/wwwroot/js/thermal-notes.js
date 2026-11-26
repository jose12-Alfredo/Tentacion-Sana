let state;
function message(text) { const node = state?.root.querySelector('[data-note-status]'); if (node) node.textContent = text; }
function download(kind) {
    const file = state?.files[kind]; if (!file) return;
    const link = document.createElement('a'); link.href = state.urls[kind]; link.download = file.name;
    document.body.append(link); link.click(); link.remove();
    message('Archivo descargado. Envíalo a tu aplicación Android de impresión; no se ha confirmado una impresión física.');
}
async function share(kind) {
    const file = state?.files[kind]; if (!file) return;
    // El File ya está preparado: navigator.share se llama durante el clic del usuario,
    // sin esperar a .NET, red o conversión de bytes que pierdan la activación transitoria.
    try { if (!navigator.share || !navigator.canShare || !navigator.canShare({ files: [file] })) { download(kind); return; } }
    catch { download(kind); return; }
    try { await navigator.share({ files: [file], title: 'Nota de entrega · Tentación Sana' }); message('Archivo compartido. La impresión se gestiona en la aplicación elegida.'); }
    catch (error) { if (error?.name !== 'AbortError') download(kind); else message('Se canceló la selección de aplicación. El archivo sigue disponible.'); }
}
function trap(event) {
    if (event.key !== 'Tab' || !state) return;
    const elements = [...state.root.querySelectorAll('button:not(:disabled),select:not(:disabled),input:not(:disabled),summary,a[href]')].filter(x => x.getClientRects().length);
    const first = elements[0], last = elements.at(-1); if (!first) return;
    if (event.shiftKey && (document.activeElement === first || !state.root.contains(document.activeElement))) { event.preventDefault(); last.focus(); }
    else if (!event.shiftKey && (document.activeElement === last || !state.root.contains(document.activeElement))) { event.preventDefault(); first.focus(); }
}
export async function prepare(id, pdfReference, pngReference, number) {
    const previousFocus = state?.previousFocus || document.activeElement;
    release(false);
    const root = document.getElementById(id); if (!root) throw new Error('El visor se cerró.');
    const [pdf, png] = await Promise.all([pdfReference.arrayBuffer(), pngReference.arrayBuffer()]);
    const files = { pdf: new File([pdf], number + '.pdf', { type: 'application/pdf' }), png: new File([png], number + '.png', { type: 'image/png' }) };
    state = { root, previousFocus, files, urls: { pdf: URL.createObjectURL(files.pdf), png: URL.createObjectURL(files.png) }, controller: new AbortController() };
    const listenerOptions = { signal: state.controller.signal };
    root.querySelectorAll('[data-note-download]').forEach(button => button.addEventListener('click', () => download(button.dataset.noteDownload), listenerOptions));
    root.querySelectorAll('[data-note-share]').forEach(button => button.addEventListener('click', () => share(button.dataset.noteShare), listenerOptions));
    root.querySelector('[data-note-print]')?.addEventListener('click', () => {
        const opened = window.open(state.urls.pdf, '_blank');
        if (!opened) download('pdf');
        else message('PDF abierto. Usa Imprimir si tu navegador tiene un servicio compatible, o comparte con tu aplicación Android. No se ha confirmado una impresión física.');
    }, listenerOptions);
    document.addEventListener('keydown', trap, listenerOptions);
    root.querySelector('.thermal-close')?.focus();
    return state.urls.png;
}
export function release(restore = true) {
    if (!state) return;
    state.controller.abort(); Object.values(state.urls).forEach(url => URL.revokeObjectURL(url));
    if (restore && state.previousFocus?.isConnected) state.previousFocus.focus();
    state = undefined;
}
