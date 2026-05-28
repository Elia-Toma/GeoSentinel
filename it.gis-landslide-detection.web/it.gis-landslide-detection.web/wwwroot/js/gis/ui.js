export function showToast(msg, type) {
    const el = document.getElementById('toast');
    if (!el) return;
    el.textContent = msg;
    el.className = 'toast show ' + (type || '');
    if (window.toastTimer) clearTimeout(window.toastTimer);
    window.toastTimer = setTimeout(() => el.className = 'toast', 3000);
}

export function initClock() {
    const el = document.getElementById('global-clock');
    if (!el) return;
    setInterval(() => {
        el.textContent = new Date().toISOString().substr(11, 8) + ' UTC';
    }, 1000);
}

export function updateStats(state) {
    const count = (d) => d?.features?.length || 0;
    const pts = document.getElementById('stat-points');
    if (pts) pts.textContent = count(state.data.points);
    
    const lns = document.getElementById('stat-lines');
    if (lns) lns.textContent = count(state.data.lines);
    
    const pls = document.getElementById('stat-polygons');
    if (pls) pls.textContent = count(state.data.polygons);
}

export function renderFeatureList(state) {
    const el = document.getElementById('feature-list');
    if (!el) return;
    let html = '';

    const addItems = (geojson, geomType) => {
        if (!geojson?.features) return;
        geojson.features.forEach(f => {
            const p = f.properties;
            const id = f.id || p.id;
            
            let icon = '⬡';
            if (geomType === 'point') {
                const pType = p.type || p.Type;
                icon = pType === 'Restaurant' ? '🍕' : pType === 'School' ? '🏫' : '📍';
            } else if (geomType === 'line') {
                const lType = p.type || p.Type;
                icon = lType === 'River' ? '🌊' : lType === 'Road' ? '🛤️' : '📏';
            }
            
            const svgTrashIcon = `
              <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="vertical-align: middle;">
                <polyline points="3 6 5 6 21 6"></polyline>
                <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                <line x1="10" y1="11" x2="10" y2="17"></line>
                <line x1="14" y1="11" x2="14" y2="17"></line>
              </svg>
            `;

            html += `<div class="feature-item" data-id="${id}" data-type="${geomType}" onclick="GIS.zoomTo(${id},'${geomType}')">
                <div><span style="margin-right:6px">${icon}</span><span class="feature-name">${p.name || 'N/A'}</span></div>
                <div class="feature-actions">
                    <button onclick="event.stopPropagation();GIS.deleteFeature(${id},'${geomType}')" title="Elimina" style="display:flex;align-items:center;justify-content:center;">
                        ${svgTrashIcon}
                    </button>
                </div>
            </div>`;
        });
    };

    addItems(state.data.points, 'point');
    addItems(state.data.lines, 'line');
    addItems(state.data.polygons, 'polygon');

    el.innerHTML = html || '<div style="color:var(--text-dim);font-size:0.7rem;">Nessun elemento caricato</div>';
}

export function renderLegend() {
    const el = document.getElementById('legend-items');
    if (!el) return;
    const items = [
        { color: '#10ffb0', label: '< 5.000' },
        { color: '#ffea00', label: '5.000 – 15.000' },
        { color: '#ff8c00', label: '15.000 – 30.000' },
        { color: '#ff4060', label: '> 30.000' }
    ];
    el.innerHTML = items.map(i =>
        `<div class="legend-item"><div class="legend-swatch" style="background:${i.color}"></div>${i.label}</div>`
    ).join('');
}

export function highlightFeature(id, type) {
    document.querySelectorAll('.feature-item').forEach(el => el.classList.remove('selected'));
    const el = document.querySelector(`.feature-item[data-id="${id}"][data-type="${type}"]`);
    if (el) { el.classList.add('selected'); el.scrollIntoView({ behavior: 'smooth', block: 'nearest' }); }
}
