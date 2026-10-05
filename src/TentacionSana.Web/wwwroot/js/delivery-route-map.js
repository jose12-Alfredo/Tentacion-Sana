const mapStates = new Map();
let leafletPromise;

export function waitForDeliveryMapStyles() {
    const loaded = [...document.styleSheets].some(sheet => sheet.href?.includes('leaflet@1.9.4/dist/leaflet.css'));
    if (loaded) return Promise.resolve();

    let link = document.querySelector('link[data-tentacion-leaflet]');
    if (!link) {
        link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
        link.dataset.tentacionLeaflet = 'true';
        document.head.appendChild(link);
    }

    if (link.sheet) return Promise.resolve();
    return new Promise((resolve, reject) => {
        link.addEventListener('load', resolve, { once: true });
        link.addEventListener('error', () => reject(new Error('No se pudieron cargar los estilos del mapa.')), { once: true });
        if (link.sheet) resolve();
    });
}

function ensureLeaflet() {
    if (window.L) return Promise.resolve(window.L);
    if (leafletPromise) return leafletPromise;

    leafletPromise = new Promise((resolve, reject) => {
        if (!document.querySelector('link[data-tentacion-leaflet]')) {
            const css = document.createElement('link');
            css.rel = 'stylesheet';
            css.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
            css.dataset.tentacionLeaflet = 'true';
            document.head.appendChild(css);
        }

        const existing = document.querySelector('script[data-tentacion-leaflet]');
        if (existing) {
            existing.addEventListener('load', () => resolve(window.L), { once: true });
            existing.addEventListener('error', reject, { once: true });
            return;
        }

        const script = document.createElement('script');
        script.src = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
        script.dataset.tentacionLeaflet = 'true';
        script.onload = () => resolve(window.L);
        script.onerror = () => reject(new Error('No se pudo cargar el mapa.'));
        document.head.appendChild(script);
    });

    return leafletPromise;
}

function extractCoordinates(location) {
    if (!location) return null;
    let decoded;
    try { decoded = decodeURIComponent(location); }
    catch { decoded = location; }

    const patterns = [
        // !3d/!4d identifica el lugar; @ identifica solamente el centro visible.
        /!3d(-?\d{1,2}(?:\.\d+)?)!4d(-?\d{1,3}(?:\.\d+)?)/i,
        /(?:q|query|destination|center)=(-?\d{1,2}(?:\.\d+)?),(-?\d{1,3}(?:\.\d+)?)/i,
        /@(-?\d{1,2}(?:\.\d+)?),(-?\d{1,3}(?:\.\d+)?)/,
        /\/(-?\d{1,2}(?:\.\d+)?),(-?\d{1,3}(?:\.\d+)?)(?:\?|$)/
    ];

    for (const pattern of patterns) {
        const match = decoded.match(pattern);
        if (!match) continue;
        const latitude = Number(match[1]);
        const longitude = Number(match[2]);
        if (Number.isFinite(latitude) && Number.isFinite(longitude) && Math.abs(latitude) <= 90 && Math.abs(longitude) <= 180) {
            return [latitude, longitude];
        }
    }
    return null;
}

async function resolveCoordinates(point) {
    if (Number.isFinite(point.latitude) && Number.isFinite(point.longitude)) {
        return [point.latitude, point.longitude];
    }
    const direct = extractCoordinates(point.location);
    if (direct) return direct;

    return null;
}

function escapeHtml(value) {
    return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}

function markerIcon(L, point) {
    const selected = Number.isInteger(point.selectedPosition);
    const state = selected ? 'selected' : point.canSelect ? 'available' : 'locked';
    const content = selected ? point.selectedPosition : '•';
    if (point.imageUrl) {
        return L.divIcon({
            className: 'route-map-div-icon',
            html: `<span class="route-map-photo-pin ${state}" style="position:relative;display:block;box-sizing:border-box;width:42px;height:42px;overflow:visible;border:3px solid #fff;border-radius:50%;background:#fff;box-shadow:0 4px 12px rgba(25,47,29,.32);transform:none"><img src="${escapeHtml(point.imageUrl)}" alt="" style="display:block;width:36px;height:36px;max-width:36px;border-radius:50%;object-fit:cover"><b style="position:absolute;right:-7px;top:-7px;display:grid;place-items:center;box-sizing:border-box;width:20px;height:20px;border:2px solid #fff;border-radius:50%;background:#247fdd;color:#fff;font:800 10px sans-serif;transform:none">${escapeHtml(content)}</b></span>`,
            iconSize: [42, 42],
            iconAnchor: [21, 21],
            popupAnchor: [0, -24]
        });
    }
    return L.divIcon({
        className: 'route-map-div-icon',
        html: `<span class="route-map-pin ${state}"><i>${content}</i></span>`,
        iconSize: [34, 42],
        iconAnchor: [17, 40],
        popupAnchor: [0, -36]
    });
}

export async function renderDeliveryRouteMap(containerId, points, dotnetReference) {
    const L = await ensureLeaflet();
    const container = document.getElementById(containerId);
    if (!container) return;

    let state = mapStates.get(containerId);
    if (!state) {
        container.innerHTML = '';
        const map = L.map(container, { zoomControl: true, scrollWheelZoom: true }).setView([-17.7833, -63.1821], 12);
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            keepBuffer: 4,
            updateWhenIdle: true,
            attribution: '&copy; OpenStreetMap'
        }).addTo(map);
        const resizeObserver = new ResizeObserver(() => map.invalidateSize({ pan: false }));
        resizeObserver.observe(container);
        state = { map, markers: L.layerGroup().addTo(map), markerByOrder: new Map(), line: null, version: 0, reference: dotnetReference, resizeObserver, signature: null };
        mapStates.set(containerId, state);
    }

    state.reference = dotnetReference;
    const signature = JSON.stringify(points.map(point => [
        point.orderId, point.latitude, point.longitude, point.location,
        point.selectedPosition, point.canSelect, point.physical, point.balance, point.imageUrl
    ]));
    if (state.signature === signature) {
        requestAnimationFrame(() => state.map.invalidateSize({ pan: false }));
        return;
    }
    state.signature = signature;
    state.version += 1;
    const version = state.version;
    state.markers.clearLayers();
    state.markerByOrder.clear();
    if (state.line) {
        state.map.removeLayer(state.line);
        state.line = null;
    }

    const resolved = [];
    for (const point of points) {
        const coordinates = await resolveCoordinates(point);
        if (version !== state.version) return;
        if (coordinates) resolved.push({ point, coordinates });
    }

    await new Promise(resolve => requestAnimationFrame(resolve));
    state.map.invalidateSize({ pan: false });
    const bounds = [];
    for (const item of resolved) {
        const { point, coordinates } = item;
        const marker = L.marker(coordinates, { icon: markerIcon(L, point), keyboard: true });
        const pointName = [point.customer, point.deliveryPoint].filter(Boolean).join(' · ');
        marker.bindTooltip(`<span class="route-pin-place"><b>${escapeHtml(pointName || `Pedido #${point.orderNumber}`)}</b></span>`, {
            permanent: true,
            direction: 'right',
            offset: [13, -19],
            className: Number.isInteger(point.selectedPosition) ? 'route-pin-label selected' : 'route-pin-label'
        });
        const instruction = point.canSelect
            ? (Number.isInteger(point.selectedPosition) ? 'Toca el pin para quitar esta parada.' : 'Toca el pin para añadir esta parada.')
            : 'Esta parada ya está en una ruta iniciada.';
        const balance = new Intl.NumberFormat('es-BO', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(point.balance ?? 0);
        const locationAction = point.mapUrl
            ? `<a href="${escapeHtml(point.mapUrl)}" target="_blank" rel="noopener noreferrer">Abrir ubicación ↗</a>`
            : '';
        marker.bindPopup(`<div class="route-map-popup"><strong>#${escapeHtml(point.orderNumber)} · ${escapeHtml(point.customer)}</strong><span>${escapeHtml(point.deliveryPoint || 'Punto de entrega')}</span><small>${escapeHtml(point.physical)} uds · Bs ${escapeHtml(balance)} pendiente</small><small>${escapeHtml(point.address || '')}${point.reference ? ` · ${escapeHtml(point.reference)}` : ''}</small>${locationAction}<small>${escapeHtml(instruction)}</small></div>`);
        if (point.canSelect) {
            marker.on('click', async () => {
                await state.reference.invokeMethodAsync('FocusOrderFromMap', point.orderId);
                await state.reference.invokeMethodAsync('ToggleStopFromMap', point.orderId);
            });
        } else {
            marker.on('click', () => state.reference.invokeMethodAsync('FocusOrderFromMap', point.orderId));
        }
        marker.addTo(state.markers);
        state.markerByOrder.set(point.orderId, marker);
        bounds.push(coordinates);
    }

    const selected = resolved
        .filter(item => Number.isInteger(item.point.selectedPosition))
        .sort((a, b) => a.point.selectedPosition - b.point.selectedPosition);
    if (selected.length > 1) {
        state.line = L.polyline(selected.map(item => item.coordinates), {
            color: '#238b2b',
            weight: 4,
            opacity: .82,
            dashArray: '9 8',
            lineJoin: 'round'
        }).addTo(state.map);
    }

    container.querySelector('.route-map-status')?.remove();
    if (resolved.length === 0) {
        const message = document.createElement('div');
        message.className = 'route-map-status';
        message.textContent = 'Estos puntos necesitan un enlace de Google Maps con ubicación exacta.';
        container.appendChild(message);
    } else {
        if (resolved.length < points.length) {
            const message = document.createElement('div');
            message.className = 'route-map-status compact';
            message.textContent = `${points.length - resolved.length} punto(s) necesitan un enlace de Google Maps con ubicación exacta.`;
            container.appendChild(message);
        }
    }
    requestAnimationFrame(() => requestAnimationFrame(() => {
        state.map.stop();
        state.map.invalidateSize({ pan: false });
        if (bounds.length > 0) state.map.fitBounds(bounds, { padding: [45, 45], maxZoom: 15, animate: false });
    }));
}

export function focusDeliveryRoutePin(containerId, orderId) {
    const state = mapStates.get(containerId);
    const marker = state?.markerByOrder.get(orderId);
    if (!state || !marker) return;
    state.map.getContainer().scrollIntoView({ behavior: 'smooth', block: 'center' });
    state.map.setView(marker.getLatLng(), Math.max(state.map.getZoom(), 16), { animate: true });
    marker.openPopup();
}

export function focusDeliveryRoutePins(containerId, orderIds) {
    const state = mapStates.get(containerId);
    if (!state) return;
    const markers = orderIds.map(id => state.markerByOrder.get(id)).filter(Boolean);
    if (markers.length === 0) return;
    state.map.getContainer().scrollIntoView({ behavior: 'smooth', block: 'center' });
    state.map.invalidateSize({ pan: false });
    if (markers.length === 1) {
        state.map.setView(markers[0].getLatLng(), Math.max(state.map.getZoom(), 16), { animate: true });
        markers[0].openPopup();
        return;
    }
    state.map.fitBounds(window.L.featureGroup(markers).getBounds(), { padding: [55, 55], maxZoom: 15, animate: true });
}

export function scrollToDeliveryElement(elementId) {
    document.getElementById(elementId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

export function refreshDeliveryRouteMapLayout(containerId) {
    const state = mapStates.get(containerId);
    if (!state) return;

    const refresh = () => {
        if (!state.map.getContainer().isConnected) return;
        state.map.invalidateSize({ pan: false });
        const markers = [...state.markerByOrder.values()];
        if (markers.length > 0) {
            state.map.fitBounds(window.L.featureGroup(markers).getBounds(), {
                padding: [45, 45],
                maxZoom: 15,
                animate: false
            });
        }
    };

    requestAnimationFrame(() => requestAnimationFrame(refresh));
    setTimeout(refresh, 180);
}

export function disposeDeliveryRouteMap(containerId) {
    const state = mapStates.get(containerId);
    if (!state) return;
    state.resizeObserver?.disconnect();
    state.map.remove();
    mapStates.delete(containerId);
}
