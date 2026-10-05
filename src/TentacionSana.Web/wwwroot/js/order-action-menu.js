const viewportPadding = 12;
const sideOffset = 6;
let listenersAttached = false;

function openMenus() {
    return document.querySelectorAll('.row-menu[open][data-menu-id]');
}

function placeMenu(details) {
    if (!details?.open) return;

    const trigger = details.querySelector(':scope > summary');
    const menu = details.querySelector(':scope > nav');
    if (!trigger || !menu) return;

    const triggerRect = trigger.getBoundingClientRect();
    const menuRect = menu.getBoundingClientRect();
    const availableBelow = window.innerHeight - triggerRect.bottom - viewportPadding;
    const availableAbove = triggerRect.top - viewportPadding;
    const openUp = availableBelow < menuRect.height + sideOffset && availableAbove > availableBelow;

    const idealTop = openUp
        ? triggerRect.top - menuRect.height - sideOffset
        : triggerRect.bottom + sideOffset;
    const top = Math.min(
        Math.max(viewportPadding, idealTop),
        Math.max(viewportPadding, window.innerHeight - menuRect.height - viewportPadding));
    const left = Math.min(
        Math.max(viewportPadding, triggerRect.right - menuRect.width),
        Math.max(viewportPadding, window.innerWidth - menuRect.width - viewportPadding));

    menu.style.top = `${Math.round(top)}px`;
    menu.style.left = `${Math.round(left)}px`;
    menu.dataset.side = openUp ? 'top' : 'bottom';
}

function repositionOpenMenus() {
    openMenus().forEach(placeMenu);
}

function closeFromOutside(event) {
    openMenus().forEach(details => {
        if (!details.contains(event.target)) details.open = false;
    });
}

function attachListeners() {
    if (listenersAttached) return;
    window.addEventListener('resize', repositionOpenMenus);
    window.addEventListener('scroll', repositionOpenMenus, true);
    document.addEventListener('pointerdown', closeFromOutside);
    listenersAttached = true;
}

export function positionOrderActionMenu(menuId) {
    attachListeners();
    const selector = `.row-menu[data-menu-id="${CSS.escape(menuId ?? '')}"]`;
    const details = document.querySelector(selector);
    if (!details?.open) return;

    openMenus().forEach(other => {
        if (other !== details) other.open = false;
    });
    requestAnimationFrame(() => placeMenu(details));
}

export function disposeOrderActionMenus() {
    if (!listenersAttached) return;
    window.removeEventListener('resize', repositionOpenMenus);
    window.removeEventListener('scroll', repositionOpenMenus, true);
    document.removeEventListener('pointerdown', closeFromOutside);
    listenersAttached = false;
}
