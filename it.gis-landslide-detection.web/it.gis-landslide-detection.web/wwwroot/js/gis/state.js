export const state = {
    map: null,
    layers: { points: null, lines: null, polygons: null },
    visible: { points: true, lines: true, polygons: true },
    data: { points: [], lines: [], polygons: [] },
    mode: null, // 'nearest' | 'within' | 'route'
    routePoints: [],
    routeMarkers: [],
    routeLayer: null,
    nearestLayer: null,
    withinLayer: null,
    searchAreaLayer: null,
    drawMeta: null
};
