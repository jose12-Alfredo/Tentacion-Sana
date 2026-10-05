window.tentacionSana = {
    prefersReducedMotion: () => window.matchMedia('(prefers-reduced-motion: reduce)').matches,
    downloadBase64: (fileName, contentType, base64) => {
        const link = document.createElement('a');
        link.download = fileName;
        link.href = `data:${contentType};base64,${base64}`;
        document.body.appendChild(link);
        link.click();
        link.remove();
    }
};

document.addEventListener('click', event => {
    if (!(event.target instanceof Element)) return;

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
