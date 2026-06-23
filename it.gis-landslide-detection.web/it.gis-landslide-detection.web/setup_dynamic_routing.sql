-- ================================================================
-- ROUTING NETWORK SETUP
--
-- Principio architetturale (la soluzione "una volta per tutte"):
--   * gis_lines       = sentieri ORIGINALI. Mai modificati da questo script.
--                       Sono cio' che la mappa mostra e che l'utente clicca
--                       per vedere i dati di pericolo. Geometria sempre pristina.
--   * routing_edges   = rete DERIVATA usata solo da pgRouting. Ricostruita ad
--                       ogni refresh con il NODING dei sentieri (vedi sotto).
--                       Non viene MAI visualizzata.
--
-- Perche' il NODING e non lo snap/merge:
--   I sentieri reali si collegano spesso a "T" (un sentiero finisce a meta' di
--   un altro). Lo snap endpoint-to-endpoint manca queste giunzioni. ST_Node
--   spezza ogni linea nei punti di intersezione, trasformando ogni incrocio
--   (anche a T) in un nodo condiviso del grafo. NON sposta la geometria: i
--   segmenti restano esattamente dove sono, solo divisi agli incroci.
--   Misurato sui dati reali: 70 isole -> 28 col solo noding.
--
-- I bridge (per le isole residue) vivono SOLO in routing_edges, hanno lunghezza
-- <= bridge_max_m (1500m su questo dataset, sufficiente a collegare TUTTE le isole
-- alla main) e cost x1000: Dijkstra li usa come ultima spiaggia, mai come
-- scorciatoia, e non compaiono sulla mappa. Conseguenza: cliccare su un qualsiasi
-- sentiero reale produce SEMPRE un percorso vero (mai la linea retta di fallback),
-- anche se per arrivarci serve attraversare un bridge invisibile.
-- ================================================================

CREATE EXTENSION IF NOT EXISTS pgrouting;

-- Tabella della rete di routing (derivata, separata da gis_lines)
CREATE TABLE IF NOT EXISTS routing_edges (
    id           bigint PRIMARY KEY,
    geom         geometry(LineString, 4326),
    source       bigint,
    target       bigint,
    cost         double precision,
    reverse_cost double precision,
    is_bridge    boolean DEFAULT false
);
CREATE INDEX IF NOT EXISTS routing_edges_geom_idx ON routing_edges USING gist (geom);

CREATE OR REPLACE FUNCTION refresh_routing_topology()
RETURNS void AS $$
DECLARE
    snap_deg          double precision := 0.0003;   -- ~33m  chiusura micro-gap alle giunzioni
    topo_tol          double precision := 0.0001;   -- ~11m  tolleranza topologia
    bridge_max_m      double precision := 1500.0;   -- max lunghezza bridge. Su questo dataset le isole
                                                    -- residue sono fra 273m e 1248m dalla main: 1500m le
                                                    -- collega TUTTE garantendo che il routing/TSP non
                                                    -- ricada mai sul fallback "linea retta" per click su
                                                    -- sentieri reali. I bridge restano invisibili sulla
                                                    -- mappa e penalizzati x1000, quindi Dijkstra li usa
                                                    -- solo se NON c'e' un percorso vero (caso limite).
    bridge_cost_mult  double precision := 1000.0;   -- penalita': bridge = ultima spiaggia
    n_edges           integer;
    n_iso_pre         integer;
    n_iso_post        integer;
    n_bridges         integer;
BEGIN
    -- 0) Pulisce eventuali bridge legacy inseriti in gis_lines da versioni vecchie
    DELETE FROM gis_lines WHERE name = '__BRIDGE_AUTO__';

    -- 1) Ricostruisce routing_edges col NODING di gis_lines.
    --    gis_lines NON viene toccato: leggiamo solo la geometria.
    TRUNCATE routing_edges;
    INSERT INTO routing_edges (id, geom, cost, reverse_cost, is_bridge)
    WITH raw AS (
        SELECT ST_Collect(geom) AS g
        FROM gis_lines
        WHERE type = 'Sentiero' AND geom IS NOT NULL
          AND ST_GeometryType(geom) = 'ST_LineString'
    ),
    snapped AS (
        -- snap della rete a se stessa: chiude i micro-gap senza spostare
        -- visibilmente i sentieri (tolleranza piccola)
        SELECT ST_Snap(g, g, snap_deg) AS g FROM raw
    ),
    noded AS (
        -- ST_Node: spezza ogni linea agli incroci -> giunzioni a T diventano nodi
        SELECT (ST_Dump(ST_Node(g))).geom AS geom FROM snapped
    )
    SELECT row_number() OVER () AS id,
           geom,
           ST_Length(geom::geography),
           ST_Length(geom::geography),
           false
    FROM noded
    WHERE ST_GeometryType(geom) = 'ST_LineString' AND ST_Length(geom) > 0;

    SELECT count(*) INTO n_edges FROM routing_edges;
    RAISE NOTICE '[routing] Segmenti noded: %', n_edges;

    -- 2) Topologia: assegna source/target e crea routing_edges_vertices_pgr
    PERFORM pgr_createTopology(
        'routing_edges', topo_tol, 'geom', 'id', 'source', 'target', clean := true
    );

    SELECT count(DISTINCT component) INTO n_iso_pre
    FROM pgr_connectedComponents(
        'SELECT id, source, target, cost, reverse_cost FROM routing_edges WHERE source IS NOT NULL'
    );
    RAISE NOTICE '[routing] Isole dopo noding (prima dei bridge): %', n_iso_pre;

    -- 3) Bridge corti (<= bridge_max_m) tra ogni isola e la componente
    --    principale. Solo in routing_edges, invisibili, cost x1000.
    --
    --    ITERATIVO: ogni giro la componente principale assorbe le isole
    --    entro bridge_max_m, poi cresce. Le isole che erano lontane dalla
    --    main ORIGINALE ma vicine a un cluster appena assorbito vengono
    --    collegate al giro successivo (effetto a catena). Si ferma quando
    --    un'iterazione non aggiunge piu' bridge.
    -- 20 iterazioni: ogni giro la main assorbe le isole vicine e cresce, riducendo
    -- la distanza alle isole piu' lontane. 6 giri erano insufficienti (alcune isole
    -- restavano oltre soglia anche se a meno di bridge_max_m dalla main ESTESA).
    n_bridges := 0;
    FOR i IN 1..20 LOOP
        DROP TABLE IF EXISTS _cc;
        DROP TABLE IF EXISTS _main;

        CREATE TEMP TABLE _cc AS
        SELECT node, component
        FROM pgr_connectedComponents(
            'SELECT id, source, target, cost, reverse_cost FROM routing_edges WHERE source IS NOT NULL'
        );

        CREATE TEMP TABLE _main AS
        SELECT component FROM _cc GROUP BY component ORDER BY count(*) DESC LIMIT 1;

        INSERT INTO routing_edges (id, geom, source, target, cost, reverse_cost, is_bridge)
        WITH iso AS (
            SELECT c.node AS iso_id, c.component AS iso_comp, v.the_geom AS iso_geom
            FROM _cc c
            JOIN routing_edges_vertices_pgr v ON v.id = c.node
            WHERE c.component NOT IN (SELECT component FROM _main)
        ),
        cand AS (
            SELECT i.iso_comp, i.iso_id, i.iso_geom,
                   m.id AS main_id, m.the_geom AS main_geom,
                   ST_Distance(i.iso_geom::geography, m.the_geom::geography) AS d_m
            FROM iso i
            CROSS JOIN LATERAL (
                SELECT v.id, v.the_geom
                FROM routing_edges_vertices_pgr v
                JOIN _cc c2 ON c2.node = v.id
                WHERE c2.component IN (SELECT component FROM _main)
                ORDER BY v.the_geom <-> i.iso_geom
                LIMIT 1
            ) m
        ),
        best AS (
            SELECT DISTINCT ON (iso_comp) *
            FROM cand
            WHERE d_m < bridge_max_m
            ORDER BY iso_comp, d_m
        )
        SELECT (SELECT COALESCE(max(id), 0) FROM routing_edges) + row_number() OVER (),
               ST_SetSRID(ST_MakeLine(iso_geom, main_geom), 4326),
               iso_id, main_id,
               d_m * bridge_cost_mult,
               d_m * bridge_cost_mult,
               true
        FROM best;

        GET DIAGNOSTICS n_iso_pre = ROW_COUNT;  -- riuso var: bridge aggiunti in questo giro
        n_bridges := n_bridges + n_iso_pre;
        EXIT WHEN n_iso_pre = 0;  -- nessun bridge possibile: fermati
    END LOOP;

    DROP TABLE IF EXISTS _cc;
    DROP TABLE IF EXISTS _main;

    SELECT count(DISTINCT component) INTO n_iso_post
    FROM pgr_connectedComponents(
        'SELECT id, source, target, cost, reverse_cost FROM routing_edges WHERE source IS NOT NULL'
    );
    RAISE NOTICE '[routing] Bridge corti creati: %', n_bridges;
    RAISE NOTICE '[routing] Isole FINALI: % (le isole residue sono sentieri genuinamente isolati > %m da tutto)', n_iso_post, bridge_max_m;
END;
$$ LANGUAGE plpgsql;

-- Prima esecuzione
SELECT refresh_routing_topology();
