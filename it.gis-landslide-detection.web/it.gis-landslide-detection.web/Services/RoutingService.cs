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
            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            // 1. Trova il nodo di partenza più vicino
            var startNode = await GetNearestNodeAsync(connection, start.X, start.Y);
            if (startNode == null) return null;

            // 2. Trova il nodo di arrivo più vicino
            var endNode = await GetNearestNodeAsync(connection, end.X, end.Y);
            if (endNode == null) return null;

            // 3. Esegui pgr_dijkstra su PostgreSQL e includi gli edge di partenza e arrivo per ritaglio preciso
            var pathGeom = await GetDijkstraPathAsync(connection, startNode.Id, endNode.Id, startNode.EdgeId, endNode.EdgeId, start, end);
            if (pathGeom == null) return null;

            // Converte il risultato in LineString (o lo estrae se è MultiLineString)
            LineString? linePath = pathGeom as LineString;
            if (linePath == null && pathGeom is MultiLineString mls && mls.Count > 0)
            {
                linePath = mls.Geometries[0] as LineString;
            }

            return new RoutingResult
            {
                Path = linePath,
                SnappedStart = linePath != null && linePath.Coordinates.Length > 0 
                    ? linePath.Coordinates[0] 
                    : new Coordinate(startNode.X, startNode.Y),
                SnappedEnd = linePath != null && linePath.Coordinates.Length > 0 
                    ? linePath.Coordinates[linePath.Coordinates.Length - 1] 
                    : new Coordinate(endNode.X, endNode.Y)
            };
        }

        private async Task<NodeInfo?> GetNearestNodeAsync(DbConnection connection, double lng, double lat)
        {
            using var command = connection.CreateCommand();
            // Trova la strada più vicina e poi prende il nodo associato più vicino, 
            // ma restituisce le coordinate proiettate sulla linea
            command.CommandText = @"
                WITH closest_edge AS (
                    SELECT id, source, target, geom
                    FROM gis_lines
                    WHERE type = 'Sentiero' AND geom IS NOT NULL AND source IS NOT NULL AND target IS NOT NULL
                    ORDER BY geom <-> ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)
                    LIMIT 1
                ),
                closest_node AS (
                    SELECT v.id, v.the_geom
                    FROM closest_edge e
                    JOIN gis_lines_vertices_pgr v ON v.id IN (e.source, e.target)
                    ORDER BY v.the_geom <-> ST_SetSRID(ST_MakePoint(@lng, @lat), 4326)
                    LIMIT 1
                )
                SELECT 
                    n.id as node_id,
                    e.id as edge_id,
                    ST_X(ST_ClosestPoint(e.geom, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326))) as x, 
                    ST_Y(ST_ClosestPoint(e.geom, ST_SetSRID(ST_MakePoint(@lng, @lat), 4326))) as y
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
                // Se la tabella gis_lines_vertices_pgr non esiste, il setup SQL non è stato eseguito
                return null;
            }
            return null;
        }

        private async Task<Geometry?> GetDijkstraPathAsync(DbConnection connection, long startNodeId, long endNodeId, long startEdgeId, long endEdgeId, Coordinate startClick, Coordinate endClick)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                WITH dijkstra_path AS (
                    SELECT g.geom
                    FROM pgr_dijkstra(
                        'SELECT id, source, target, cost, reverse_cost FROM gis_lines WHERE type = ''Sentiero'' AND geom IS NOT NULL AND source IS NOT NULL',
                        @startNode,
                        @endNode,
                        directed := false
                    ) AS p
                    JOIN gis_lines AS g ON p.edge = g.id
                    WHERE p.edge > 0
                ),
                all_geoms AS (
                    SELECT geom FROM dijkstra_path
                    UNION
                    SELECT geom FROM gis_lines WHERE id IN (@startEdge, @endEdge)
                ),
                path_query AS (
                    SELECT ST_LineMerge(ST_Collect(geom)) AS path_geom
                    FROM all_geoms
                ),
                sliced_path AS (
                    SELECT path_geom,
                           ST_LineLocatePoint(path_geom, ST_SetSRID(ST_MakePoint(@startLng, @startLat), 4326)) AS f_start,
                           ST_LineLocatePoint(path_geom, ST_SetSRID(ST_MakePoint(@endLng, @endLat), 4326)) AS f_end
                    FROM path_query
                )
                SELECT ST_AsText(
                    ST_LineSubstring(
                        path_geom,
                        LEAST(f_start, f_end),
                        GREATEST(f_start, f_end)
                    )
                ) AS path_wkt
                FROM sliced_path;
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
                return _wktReader.Read(wkt);
            }
            return null;
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
