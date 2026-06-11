export const state = {
    map: null,
    layers: { points: null, lines: null, polygons: null },
    visible: { points: true, lines: true, polygons: true },
    data: { points: [], lines: [], polygons: [] },
    mode: null, // 'nearest' | 'within' | 'route' | 'intersection'
    routePoints: [],
    routeMarkers: [],
    routeLayer: null,
    nearestLayer: null,
    withinLayer: null,
    intersectionLayer: null,
    searchAreaLayer: null,
    drawMeta: null,
    isEditMode: false
};
