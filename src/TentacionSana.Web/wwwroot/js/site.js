window.tentacionSana = {
    prefersReducedMotion: () => window.matchMedia('(prefers-reduced-motion: reduce)').matches,
    downloadStream: async (fileName, contentType, contentStreamReference) => {
        const arrayBuffer = await contentStreamReference.arrayBuffer();
        const url = URL.createObjectURL(new Blob([arrayBuffer], { type: contentType }));
        try {
            const link = document.createElement('a');
            link.download = fileName;
            link.href = url;
            document.body.appendChild(link);
            link.click();
            link.remove();
        } finally {
            window.setTimeout(() => URL.revokeObjectURL(url), 1000);
        }
    }
};

document.addEventListener('components-reconnect-state-changed', event => {
    if (event.detail?.state !== 'rejected') return;

    // Render replaced the server process and the old Blazor circuit no longer exists.
    // Reloading creates a new circuit instead of leaving a visible but inert page.
    window.location.reload();
});

document.addEventListener('click', event => {
    if (!(event.target instanceof Element)) return;

    if (event.target.closest('.admin-navigation nav a')) {
        const menu = document.getElementById('admin-menu-toggle');
        if (menu instanceof HTMLInputElement) menu.checked = false;
    }

    const toggle = event.target.closest('[data-password-toggle]');
    if (!toggle) return;

    const inputId = toggle.getAttribute('aria-controls');
    const input = inputId ? document.getElementById(inputId) : null;
    if (!(input instanceof HTMLInputElement)) return;

    const shouldShow = input.type === 'password';
    input.type = shouldShow ? 'text' : 'password';
    toggle.setAttribute('aria-pressed', shouldShow.toString());
    toggle.setAttribute('aria-label', shouldShow ? 'Ocultar contraseña' : 'Mostrar contraseña');
    input.focus({ preventScroll: true });
});

document.addEventListener('keydown', event => {
    if (event.key !== 'Escape') return;
    const menu = document.getElementById('admin-menu-toggle');
    if (menu instanceof HTMLInputElement && menu.checked) {
        menu.checked = false;
        menu.focus();
    }
});

document.addEventListener('submit', event => {
    if (!(event.target instanceof HTMLFormElement) || !event.target.matches('[data-login-form]')) return;

    const form = event.target;
    if (form.dataset.submitting === 'true') {
        event.preventDefault();
        return;
    }

    form.dataset.submitting = 'true';
    form.setAttribute('aria-busy', 'true');
    const submitButton = form.querySelector('[data-login-submit]');
    if (submitButton instanceof HTMLButtonElement) submitButton.disabled = true;
});

window.addEventListener('pageshow', () => {
    document.querySelectorAll('[data-login-form]').forEach(form => {
        form.removeAttribute('data-submitting');
        form.removeAttribute('aria-busy');
        const submitButton = form.querySelector('[data-login-submit]');
        if (submitButton instanceof HTMLButtonElement) submitButton.disabled = false;
    });
});
