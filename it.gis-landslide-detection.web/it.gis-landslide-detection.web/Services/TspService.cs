using NetTopologySuite.Geometries;

namespace it.gis_landslide_detection.web.Services
{
    public class TspResult
    {
        public LineString? FullPath { get; set; }
        public List<Coordinate> OrderedPoints { get; set; } = new();
        public List<int> VisitOrder { get; set; } = new();
        public double TotalLengthDeg { get; set; }
    }

    public interface ITspService
    {
        Task<TspResult?> SolveAsync(List<Coordinate> pois, bool closed);
    }

    /// <summary>
    /// Travelling Salesman Problem solver basato su Nearest-Neighbor + 2-opt.
    /// Le distanze tra POI sono calcolate sul grafo dei sentieri tramite Dijkstra
    /// (delegato a IRoutingService), non come distanze euclidee.
    /// </summary>
    public class TspService : ITspService
    {
        private readonly IRoutingService _routing;
        private readonly GeometryFactory _geometryFactory;

        public TspService(IRoutingService routing)
        {
            _routing = routing;
            _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        }

        public async Task<TspResult?> SolveAsync(List<Coordinate> pois, bool closed)
        {
            if (pois == null || pois.Count < 2) return null;
            int n = pois.Count;

            // 1. Matrice distanze + cache dei path tra coppie (Dijkstra reale sul grafo)
            var dist = new double[n, n];
            var paths = new LineString?[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j) { dist[i, j] = 0; continue; }
                    var res = await _routing.CalculateShortestPathAsync(pois[i], pois[j]);
                    // Usa il costo Dijkstra reale (metri, penalità bridge ×1000 inclusa).
                    // NON usare res.Path.Length: i bridge hanno geometria corta ma costo alto,
                    // e il TSP ottimizzerebbe su percorsi aerei invece dei sentieri reali.
                    // RouteDistanceM = MaxValue/2 quando il routing è caduto in fallback.
                    dist[i, j] = res.RouteDistanceM;
                    paths[i, j] = res.Path;
                }
            }

            // 2. Nearest-Neighbor — soluzione iniziale partendo dal primo POI
            var order = NearestNeighbor(dist, n);

            // 3. 2-opt — miglioramento iterativo
            order = TwoOpt(order, dist, closed);

            // 4. Ricostruisce il path completo concatenando le polilinee Dijkstra
            var allCoords = new List<Coordinate>();
            double total = 0;
            int legs = closed ? n : n - 1;
            for (int k = 0; k < legs; k++)
            {
                int a = order[k];
                int b = order[(k + 1) % n];
                total += dist[a, b];
                var seg = paths[a, b];
                if (seg == null) continue;
                var coords = seg.Coordinates;
                if (allCoords.Count == 0) allCoords.AddRange(coords);
                else allCoords.AddRange(coords.Skip(1));
            }

            LineString? full = allCoords.Count >= 2
                ? _geometryFactory.CreateLineString(allCoords.ToArray())
                : null;

            return new TspResult
            {
                FullPath = full,
                OrderedPoints = order.Select(i => pois[i]).ToList(),
                VisitOrder = order,
                TotalLengthDeg = total
            };
        }

        private static List<int> NearestNeighbor(double[,] dist, int n)
        {
            var visited = new bool[n];
            var order = new List<int> { 0 };
            visited[0] = true;
            for (int step = 1; step < n; step++)
            {
                int last = order[^1];
                int best = -1;
                double bestD = double.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (visited[j]) continue;
                    if (dist[last, j] < bestD) { bestD = dist[last, j]; best = j; }
                }
                if (best < 0) break;
                visited[best] = true;
                order.Add(best);
            }
            return order;
        }

        private static List<int> TwoOpt(List<int> order, double[,] dist, bool closed)
        {
            int n = order.Count;
            if (n < 4) return order;
            bool improved = true;
            int guard = 0;
            while (improved && guard++ < 100)
            {
                improved = false;
                for (int i = 0; i < n - 1; i++)
                {
                    for (int k = i + 1; k < n; k++)
                    {
                        if (!closed && k == n - 1 && i == 0) continue;
                        double before = SegmentCost(order, i, k, dist, closed, n);
                        var swapped = TwoOptSwap(order, i, k);
                        double after = SegmentCost(swapped, i, k, dist, closed, n);
                        if (after + 1e-12 < before)
                        {
                            order = swapped;
                            improved = true;
                        }
                    }
                }
            }
            return order;
        }

        private static double SegmentCost(List<int> tour, int i, int k, double[,] dist, bool closed, int n)
        {
            double sum = 0;
            int legs = closed ? n : n - 1;
            for (int t = 0; t < legs; t++)
            {
                int a = tour[t];
                int b = tour[(t + 1) % n];
                sum += dist[a, b];
            }
            return sum;
        }

        private static List<int> TwoOptSwap(List<int> tour, int i, int k)
        {
            var result = new List<int>(tour.Count);
            for (int t = 0; t < i; t++) result.Add(tour[t]);
            for (int t = k; t >= i; t--) result.Add(tour[t]);
            for (int t = k + 1; t < tour.Count; t++) result.Add(tour[t]);
            return result;
        }
    }
}
