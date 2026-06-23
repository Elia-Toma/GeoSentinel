using System.Data;
using System.Data.Common;
using it.gis_landslide_detection.web.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace it.gis_landslide_detection.web.Services
{
    public class RoutingResult
    {
        public LineString? Path { get; set; }
        public Coordinate? SnappedStart { get; set; }
        public Coordinate? SnappedEnd { get; set; }
        /// <summary>
        /// Costo Dijkstra aggregato in metri (include penalità bridge ×1000).
        /// Usare questo nel TSP, NON Path.Length — i bridge hanno geometria corta
        /// ma costo alto, e il TSP deve riflettere questo per evitare archi aerei.
        /// </summary>
        public double RouteDistanceM { get; set; }
        public bool UsedBridge { get; set; }
    }

    public interface IRoutingService
    {
        Task<RoutingResult?> CalculateShortestPathAsync(Coordinate start, Coordinate end);
    }

    public class RoutingService : IRoutingService
    {
        private readonly ApplicationDbContext _context;
        private readonly GeometryFactory _geometryFactory;
        private readonly WKTReader _wktReader;

        public RoutingService(ApplicationDbContext context)
        {
            _context = context;
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            _wktReader = new WKTReader(_geometryFactory);
        }

        public async Task<RoutingResult?> CalculateShortestPathAsync(Coordinate start, Coordinate end)
        {
            // NB: la connection appartiene al DbContext — NON usare `using`, altrimenti
            // viene disposta dopo la prima chiamata e i loop (es. TSP) crashano.
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // 1. Trova i nodi di partenza/arrivo più vicini sul grafo
            var startNode = await GetNearestNodeAsync(connection, start.X, start.Y);
            var endNode = await GetNearestNodeAsync(connection, end.X, end.Y);

            // 2. Tenta Dijkstra solo se abbiamo entrambi i nodi sul grafo
            LineString? linePath = null;
            double routeDistanceM = double.MaxValue / 2;
            bool usedBridge = false;
            if (startNode != null && endNode != null)
            {
                var (pathGeom, costM, hasBridge) = await GetDijkstraPathAsync(connection, startNode.Id, endNode.Id, startNode.EdgeId, endNode.EdgeId, start, end);
                linePath = pathGeom as LineString;
                if (linePath == null && pathGeom is MultiLineString mls && mls.Count > 0)
                {
                    var coords = new List<Coordinate>();
                    foreach (var g in mls.Geometries)
                    {
                        if (g is not LineString ls) continue;
                        foreach (var c in ls.Coordinates)
                        {
                            if (coords.Count == 0 || !coords[^1].Equals2D(c)) coords.Add(c);
                        }
                    }
                    if (coords.Count >= 2) linePath = _geometryFactory.CreateLineString(coords.ToArray());
                }
                if (linePath != null && linePath.Coordinates.Length >= 2)
                {
                    routeDistanceM = costM;
                    usedBridge = hasBridge;
                }
            }

            // 3. Fallback: se Dijkstra non ha prodotto un path (componenti irraggiungibili,
            //    topologia non inizializzata, o punti totalmente fuori dalla rete).
            //    RouteDistanceM rimane MaxValue/2: il TSP eviterà questo arco invece di
            //    ottimizzare su una distanza euclidea fasulla.
            if (linePath == null || linePath.Coordinates.Length < 2)
            {
                var fallbackStart = startNode != null ? new Coordinate(startNode.X, startNode.Y) : start;
                var fallbackEnd = endNode != null ? new Coordinate(endNode.X, endNode.Y) : end;
                linePath = _geometryFactory.CreateLineString(new[] { fallbackStart, fallbackEnd });
            }

            // 4. Risolvi orientamento: ST_LineMerge non garantisce la direzione del LineString,
            //    quindi Coordinates[0] potrebbe essere il punto di arrivo invece che di partenza.
            //    Confrontiamo distanza dei due estremi del path al click originale.
            var first = linePath.Coordinates[0];
            var last = linePath.Coordinates[^1];
            double dFirstToStart = SqrDist(first, start);
            double dLastToStart = SqrDist(last, start);
            return new RoutingResult
            {
                Path = linePath,
                SnappedStart = dFirstToStart <= dLastToStart ? first : last,
                SnappedEnd   = dFirstToStart <= dLastToStart ? last  : first,
                RouteDistanceM = routeDistanceM,
                UsedBridge = usedBridge
            };
        }

        private static double SqrDist(Coordinate a, Coordinate b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        // Soglia max (in gradi) per accettare lo snap del click su un sentiero.
        // ~0.003 deg ≈ 330m alla latitudine italiana. Se il sentiero REALE piu'
        // vicino e' oltre questa distanza, lo snap viene rifiutato: significa che
        // l'utente ha cliccato su un sentiero che non esiste in gis_lines (es. e'
        // solo nel raster di base tipo OpenStreetMap) e snapparlo a chilometri di
        // distanza darebbe un risultato fuorviante.
        private const double MaxSnapDistanceDeg = 0.003;

        private async Task<NodeInfo?> GetNearestNodeAsync(DbConnection connection, double lng, double lat)
        {
            using var command = connection.CreateCommand();
            // Cerca sul grafo di routing (routing_edges = rete noded derivata dai
            // sentieri), NON su gis_lines. Esclude i bridge (is_bridge) perche' sono
            // connettori artificiali corti che non devono "catturare" il click.
            // Restituisce edge_id, nodo piu' vicino (source/target) e proiezione del
            // click sul segmento. Filtra i segmenti troppo lontani dal click: meglio
            // nessuno snap che uno snap a chilometri (vedi MaxSnapDistanceDeg).
            command.CommandText = @"
                WITH closest_edge AS (
                    SELECT id, source, target, geom,
                           ST_Distance(geom, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)) AS d_deg
                    FROM routing_edges
                    WHERE geom IS NOT NULL
                      AND source IS NOT NULL
                      AND target IS NOT NULL
                      AND is_bridge = false
                    ORDER BY geom <-> ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)
                    LIMIT 1
                ),
                closest_node AS (
                    SELECT v.id, v.the_geom
                    FROM closest_edge e
                    JOIN routing_edges_vertices_pgr v ON v.id IN (e.source, e.target)
                    ORDER BY v.the_geom <-> ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)
                    LIMIT 1
                )
                SELECT
                    n.id as node_id,
                    e.id as edge_id,
                    ST_X(ST_ClosestPoint(e.geom, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326))) as x,
                    ST_Y(ST_ClosestPoint(e.geom, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326))) as y,
                    e.d_deg as dist_deg
                FROM closest_edge e
                CROSS JOIN closest_node n;
            ";
            
            var pLng = command.CreateParameter();
            pLng.ParameterName = "@lng";
            pLng.Value = lng;
            command.Parameters.Add(pLng);

            var pLat = command.CreateParameter();
            pLat.ParameterName = "@lat";
            pLat.Value = lat;
            command.Parameters.Add(pLat);

            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    double distDeg = reader.GetDouble(4);
                    // Click troppo lontano da qualsiasi sentiero reale → rifiuto lo snap.
                    // Il chiamante (CalculateShortestPathAsync) ha gia' il fallback che
                    // disegna una linea retta dal click vero al click vero.
                    if (distDeg > MaxSnapDistanceDeg)
                    {
                        return null;
                    }
                    return new NodeInfo
                    {
                        Id = reader.GetInt64(0),
                        EdgeId = reader.GetInt64(1),
                        X = reader.GetDouble(2),
                        Y = reader.GetDouble(3)
                    };
                }
            }
            catch (Exception)
            {
                // Se la tabella routing_edges (o ..._vertices_pgr) non esiste, il setup SQL non è stato eseguito
                return null;
            }
            return null;
        }

        private async Task<(Geometry? geom, double costM, bool usedBridge)> GetDijkstraPathAsync(DbConnection connection, long startNodeId, long endNodeId, long startEdgeId, long endEdgeId, Coordinate startClick, Coordinate endClick)
        {
            using var command = connection.CreateCommand();
            // Ricostruzione del path robusta ai micro-gap della topologia.
            //
            // Problema del vecchio approccio "ST_LineMerge(ST_Collect(...))":
            //   pgr_createTopology assegna lo STESSO nodo a estremità vicine entro la
            //   tolleranza (~11m) ma NON sposta la geometria. Quindi due edge consecutivi
            //   in un path Dijkstra condividono il nodo nel grafo ma le loro geometrie
            //   distano fino a ~10m. ST_LineMerge unisce solo linee con estremi ESATTAMENTE
            //   coincidenti → su quasi tutti i path (misurato: 59/60 nella componente
            //   principale) restituisce un MultiLineString, e il C# teneva solo il primo
            //   frammento → percorso troncato/spezzato anche su sentieri ben connessi.
            //
            // Soluzione: ordino gli edge per seq di Dijkstra, oriento ciascuno nella
            // direzione di percorrenza (ST_Reverse se serve) e li concateno con
            // ST_MakeLine(... ORDER BY seq). ST_MakeLine NON richiede estremi coincidenti:
            // colma i gap <=11m con connettori rettilinei impercettibili e produce SEMPRE
            // un unico LineString. Poi taglio il path alle proiezioni dei click:
            //   - se l'edge del click È percorso da Dijkstra (primo/ultimo) → taglio il
            //     node_path alla frazione della proiezione;
            //   - se NON è percorso (click su un edge laterale) → aggancio un connettore
            //     corto proiezione→nodo prependendo/appendendo la proiezione.
            command.CommandText = @"
                WITH
                params AS (
                    SELECT ST_SetSRID(ST_MakePoint(@startLng, @startLat), 4326) AS sp,
                           ST_SetSRID(ST_MakePoint(@endLng,   @endLat),   4326) AS ep
                ),
                -- Proiezioni dei click sui rispettivi edge (punti sul sentiero, on-trail)
                proj AS (
                    SELECT (SELECT ST_ClosestPoint(geom, p.sp) FROM routing_edges WHERE id = @startEdge) AS proj_start,
                           (SELECT ST_ClosestPoint(geom, p.ep) FROM routing_edges WHERE id = @endEdge)   AS proj_end
                    FROM params p
                ),
                -- Caso 1: start ed end sullo stesso edge → slice diretto, niente Dijkstra
                same_edge AS (
                    SELECT ST_LineSubstring(
                               g.geom,
                               LEAST(   ST_LineLocatePoint(g.geom, p.sp), ST_LineLocatePoint(g.geom, p.ep)),
                               GREATEST(ST_LineLocatePoint(g.geom, p.sp), ST_LineLocatePoint(g.geom, p.ep))
                           ) AS geom
                    FROM routing_edges g, params p
                    WHERE g.id = @startEdge AND @startEdge = @endEdge
                ),
                -- Caso 2: edge diversi → Dijkstra fra i nodi, ogni edge ORIENTATO nel
                -- verso di percorrenza (parte da d.node)
                dijkstra AS (
                    SELECT d.seq, d.node, d.edge, d.cost AS edge_cost, g.is_bridge,
                           CASE WHEN g.source = d.node THEN g.geom ELSE ST_Reverse(g.geom) END AS og
                    FROM pgr_dijkstra(
                        'SELECT id, source, target, cost, reverse_cost FROM routing_edges WHERE source IS NOT NULL',
                        @startNode, @endNode, directed := false
                    ) AS d
                    JOIN routing_edges g ON g.id = d.edge
                    WHERE d.edge > 0 AND @startEdge <> @endEdge
                ),
                -- Costo aggregato Dijkstra (metri reali + penalità bridge ×1000)
                -- Il TSP deve usare questo, non la lunghezza geometrica del path
                route_stats AS (
                    SELECT COALESCE(sum(edge_cost), 0) AS total_cost_m,
                           bool_or(is_bridge) AS has_bridge
                    FROM dijkstra
                ),
                node_path AS (SELECT ST_MakeLine(og ORDER BY seq) AS geom FROM dijkstra),
                fe AS (SELECT edge FROM dijkstra ORDER BY seq ASC  LIMIT 1),
                le AS (SELECT edge FROM dijkstra ORDER BY seq DESC LIMIT 1),
                -- Taglia il node_path alle proiezioni dei click, ma SOLO sull'edge
                -- iniziale/finale effettivamente percorso; altrimenti tieni l'estremo (0/1)
                trimmed AS (
                    SELECT ST_LineSubstring(np.geom, LEAST(f.fs, f.fe), GREATEST(f.fs, f.fe)) AS geom
                    FROM node_path np, proj pr,
                    LATERAL (SELECT
                        CASE WHEN (SELECT edge FROM fe) = @startEdge THEN ST_LineLocatePoint(np.geom, pr.proj_start) ELSE 0 END AS fs,
                        CASE WHEN (SELECT edge FROM le) = @endEdge   THEN ST_LineLocatePoint(np.geom, pr.proj_end)   ELSE 1 END AS fe
                    ) f
                    WHERE np.geom IS NOT NULL AND ST_GeometryType(np.geom) = 'ST_LineString' AND ST_NumPoints(np.geom) >= 2
                ),
                -- Prepende/appende la proiezione del click: se l'edge È percorso il punto
                -- coincide con l'estremo del trim (duplicato innocuo); se NON è percorso
                -- crea il connettore corto proiezione→nodo.
                multi_path AS (
                    SELECT ST_MakeLine(ARRAY[pr.proj_start, t.geom, pr.proj_end]) AS geom
                    FROM trimmed t, proj pr
                )
                SELECT ST_AsText(r.geom), r.cost_m, r.has_bridge
                FROM (
                    SELECT geom,
                           ST_Length(geom::geography) AS cost_m,
                           false AS has_bridge
                    FROM same_edge WHERE geom IS NOT NULL
                    UNION ALL
                    SELECT geom,
                           (SELECT total_cost_m FROM route_stats) AS cost_m,
                           (SELECT has_bridge    FROM route_stats) AS has_bridge
                    FROM multi_path WHERE geom IS NOT NULL
                ) r
                WHERE r.geom IS NOT NULL
                  AND ST_GeometryType(r.geom) = 'ST_LineString'
                  AND ST_NumPoints(r.geom) >= 2;
            ";

            var pStart = command.CreateParameter();
            pStart.ParameterName = "@startNode";
            pStart.Value = startNodeId;
            command.Parameters.Add(pStart);

            var pEnd = command.CreateParameter();
            pEnd.ParameterName = "@endNode";
            pEnd.Value = endNodeId;
            command.Parameters.Add(pEnd);

            var pStartEdge = command.CreateParameter();
            pStartEdge.ParameterName = "@startEdge";
            pStartEdge.Value = startEdgeId;
            command.Parameters.Add(pStartEdge);

            var pEndEdge = command.CreateParameter();
            pEndEdge.ParameterName = "@endEdge";
            pEndEdge.Value = endEdgeId;
            command.Parameters.Add(pEndEdge);

            var pStartLng = command.CreateParameter();
            pStartLng.ParameterName = "@startLng";
            pStartLng.Value = startClick.X;
            command.Parameters.Add(pStartLng);

            var pStartLat = command.CreateParameter();
            pStartLat.ParameterName = "@startLat";
            pStartLat.Value = startClick.Y;
            command.Parameters.Add(pStartLat);

            var pEndLng = command.CreateParameter();
            pEndLng.ParameterName = "@endLng";
            pEndLng.Value = endClick.X;
            command.Parameters.Add(pEndLng);

            var pEndLat = command.CreateParameter();
            pEndLat.ParameterName = "@endLat";
            pEndLat.Value = endClick.Y;
            command.Parameters.Add(pEndLat);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync() && !reader.IsDBNull(0))
            {
                string wkt = reader.GetString(0);
                double costM = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                bool hasBridge = !reader.IsDBNull(2) && reader.GetBoolean(2);
                return (_wktReader.Read(wkt), costM, hasBridge);
            }
            return (null, 0, false);
        }

        private class NodeInfo
        {
            public long Id { get; set; }
            public long EdgeId { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }
    }
}
