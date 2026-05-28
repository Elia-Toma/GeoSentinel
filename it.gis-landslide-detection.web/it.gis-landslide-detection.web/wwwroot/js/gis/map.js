import { state } from './state.js';

export function initMap(onMapClick, onDrawCreated) {
    state.map = L.map('map', { zoomControl: false }).setView([43.1, 13.4], 12);
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

export function choroplethStyle(pop) {
    let fillColor = '#1e3a5f';
    if (pop > 30000) fillColor = '#ff4060';
    else if (pop > 15000) fillColor = '#ff8c00';
    else if (pop > 5000) fillColor = '#ffea00';
    else fillColor = '#10ffb0';
    return { fillColor, color: '#0f172a', weight: 2, fillOpacity: 0.45 };
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
        return `<div style="${style}"><b>${props.name || 'N/A'}</b><br/>Pop: ${(props.population || 0).toLocaleString()}${deleteBtn}</div>`;
    }
    return `<div style="${style}"><b>${props.name || 'N/A'}</b><br/>Tipo: ${props.type || '—'}${deleteBtn}</div>`;
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
    if (state.searchAreaLayer) { state.map.removeLayer(state.searchAreaLayer); state.searchAreaLayer = null; }
    state.map.pm.disableDraw();
    
    const rs = document.getElementById('routing-status');
    if (rs) rs.classList.remove('active');
    
    document.querySelectorAll('#btn-nearest,#btn-within,#btn-route').forEach(b => b.classList.remove('active'));
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
