import { state } from './state.js';

export function initMap(onMapClick, onDrawCreated) {
    // Centro mappa: Camerino / area Sibillini
    state.map = L.map('map', { zoomControl: false }).setView([43.095, 13.075], 12);
    L.control.zoom({ position: 'topleft' }).addTo(state.map);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 18 }).addTo(state.map);

    state.map.on('mousemove', e => {
        const cb = document.getElementById('coord-bar');
        if (cb) cb.textContent = `LAT: ${e.latlng.lat.toFixed(4)} | LNG: ${e.latlng.lng.toFixed(4)}`;
    });

    state.map.on('click', onMapClick);
    state.map.on('pm:create', onDrawCreated);

    // Init Geoman controls (hidden by default)
    state.map.pm.addControls({ position: 'topleft', drawMarker: false, drawPolyline: false,
        drawPolygon: false, drawCircle: false, drawCircleMarker: false, drawRectangle: false,
        drawText: false, editMode: false, dragMode: false, cutPolygon: false, removalMode: false,
        rotateMode: false });
}

/**
 * Stile coropletico per zone di rischio frana (indice 0-100, scala IFFI).
 * Critico ≥ 75: rosso (#ff4060) — es. Colamento rapido (100)
 * Alto   ≥ 50: arancione (#ff8c00) — es. Crollo/Ribaltamento (80)
 * Medio  ≥ 25: giallo (#ffea00) — es. Scivolamento (60) / Complesso (40)
 * Basso   < 25: verde (#10ffb0) — Bassa pericolosità (20)
 */
export function choroplethStyle(riskLevel) {
    let fillColor;
    if (riskLevel >= 75)      fillColor = '#ff4060'; // Critico
    else if (riskLevel >= 50) fillColor = '#ff8c00'; // Alto
    else if (riskLevel >= 25) fillColor = '#ffea00'; // Medio
    else                      fillColor = '#10ffb0'; // Basso
    return { fillColor, color: '#0f172a', weight: 2, fillOpacity: 0.50 };
}

export function makePopup(type, props) {
    const style = 'font-family:DM Mono,monospace;font-size:12px;color:#cbd5e1;';
    
    const deleteBtn = `
      <div style="margin-top:8px;border-top:1px solid #334155;padding-top:6px;display:flex;justify-content:flex-end;">
        <button onclick="event.stopPropagation();GIS.deleteFeature(${props.id},'${type}')" 
                title="Elimina" 
                style="background:none;border:none;color:#ff4060;cursor:pointer;padding:2px 4px;font-size:11px;display:flex;align-items:center;gap:4px;font-family:inherit;">
          <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="stroke:#ff4060;">
            <polyline points="3 6 5 6 21 6"></polyline>
            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
          </svg>
          Elimina
        </button>
      </div>
    `;

    if (type === 'polygon') {
        const riskLevel = props.riskLevel ?? props.population ?? 0;
        const riskLabel = riskLevel >= 75 ? '🔴 Critico' : riskLevel >= 50 ? '🟠 Alto' : riskLevel >= 25 ? '🟡 Medio' : '🟢 Basso';
        return `<div style="${style}"><b>${props.name || 'N/A'}</b><br/>Rischio: <b>${riskLevel}/100</b> — ${riskLabel}${deleteBtn}</div>`;
    }
    if (type === 'line') {
        const typeIcon = props.type === 'Sentiero' ? '🥾' : props.type === 'Torrente' ? '🌊' : props.type === 'Fiume' ? '🏞️' : '📏';
        return `<div style="${style}"><b>${props.name || 'N/A'}</b><br/>${typeIcon} ${props.type || '—'}${deleteBtn}</div>`;
    }
    // point
    const typeIcon = props.type === 'Baita' ? '🏔️' : props.type === 'PuntoRistoro' ? '☕' : '📍';
    return `<div style="${style}"><b>${props.name || 'N/A'}</b><br/>${typeIcon} ${props.type || '—'}${deleteBtn}</div>`;
}

export function clearMode() {
    state.mode = null;
    state.routePoints = [];
    // Remove route point markers
    if (state.routeMarkers) {
        state.routeMarkers.forEach(m => state.map.removeLayer(m));
        state.routeMarkers = [];
    }
    if (state.routeLayer) { state.map.removeLayer(state.routeLayer); state.routeLayer = null; }
    if (state.nearestLayer) { state.map.removeLayer(state.nearestLayer); state.nearestLayer = null; }
    if (state.withinLayer) { state.map.removeLayer(state.withinLayer); state.withinLayer = null; }
    if (state.intersectionLayer) { state.map.removeLayer(state.intersectionLayer); state.intersectionLayer = null; }
    if (state.searchAreaLayer) { state.map.removeLayer(state.searchAreaLayer); state.searchAreaLayer = null; }
    state.map.pm.disableDraw();
    
    const rs = document.getElementById('routing-status');
    if (rs) rs.classList.remove('active');
    
    document.querySelectorAll('#btn-nearest,#btn-within,#btn-route,#btn-intersection').forEach(b => b.classList.remove('active'));
}

export function zoomToFeature(id, type) {
    const layerGroup = type === 'point' ? state.layers.points : type === 'line' ? state.layers.lines : state.layers.polygons;
    if (!layerGroup) return;
    layerGroup.eachLayer(layer => {
        if ((layer.feature?.id || layer.feature?.properties?.id) === id) {
            if (layer.getBounds) state.map.fitBounds(layer.getBounds(), { padding: [50, 50] });
            else if (layer.getLatLng) state.map.flyTo(layer.getLatLng(), 15);
            layer.openPopup();
        }
    });
}
