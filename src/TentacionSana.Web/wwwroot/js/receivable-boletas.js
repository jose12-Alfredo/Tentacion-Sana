let dialog;
let previousFocus;

function trapTab(event) {
    if (event.key !== 'Tab' || !dialog) return;
    const controls = [...dialog.querySelectorAll('button:not(:disabled), a[href], input:not(:disabled), [tabindex="0"]')]
        .filter(element => element.getClientRects().length);
    if (!controls.length) return;
    const first = controls[0];
    const last = controls[controls.length - 1];
    if (event.shiftKey && (document.activeElement === first || !dialog.contains(document.activeElement))) {
        event.preventDefault();
        last.focus();
    } else if (!event.shiftKey && (document.activeElement === last || !dialog.contains(document.activeElement))) {
        event.preventDefault();
        first.focus();
    }
}

export function setActive(active) {
    if (active) {
        if (dialog) return;
        dialog = document.getElementById('receivable-boleta-dialog');
        if (!dialog) return;
        previousFocus = document.activeElement;
        document.addEventListener('keydown', trapTab);
        dialog.querySelector('button')?.focus();
    } else {
        document.removeEventListener('keydown', trapTab);
        dialog = undefined;
        if (previousFocus?.isConnected) previousFocus.focus();
        previousFocus = undefined;
    }
}
