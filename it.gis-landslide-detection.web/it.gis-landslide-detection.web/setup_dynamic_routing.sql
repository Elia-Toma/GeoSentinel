-- 1. Abilita pgRouting (se non lo hai già fatto)
CREATE EXTENSION IF NOT EXISTS pgrouting;

-- 2. Aggiunge le colonne necessarie a pgRouting direttamente sulla tabella gis_lines
ALTER TABLE gis_lines ADD COLUMN IF NOT EXISTS source bigint;
ALTER TABLE gis_lines ADD COLUMN IF NOT EXISTS target bigint;
ALTER TABLE gis_lines ADD COLUMN IF NOT EXISTS cost double precision;
ALTER TABLE gis_lines ADD COLUMN IF NOT EXISTS reverse_cost double precision;

-- 3. Funzione per ricalcolare tutta la topologia e i costi
-- Questa funzione viene chiamata automaticamente quando vengono aggiunti, modificati o rimossi sentieri.
CREATE OR REPLACE FUNCTION refresh_routing_topology()
RETURNS void AS $$
BEGIN
    -- Imposta il costo (lunghezza in metri, castando a geography)
    UPDATE gis_lines 
    SET cost = ST_Length(geom::geography),
        reverse_cost = ST_Length(geom::geography)
    WHERE type = 'Sentiero' AND geom IS NOT NULL;

    -- Crea la topologia (crea la tabella gis_lines_vertices_pgr con i nodi/incroci)
    -- Tolleranza: 0.0001 gradi (circa 11 metri)
    PERFORM pgr_createTopology(
        'gis_lines', 
        0.0001, 
        'geom', 
        'id', 
        'source', 
        'target', 
        rows_where := 'type = ''Sentiero'' AND geom IS NOT NULL',
        clean := true
    );
END;
$$ LANGUAGE plpgsql;

-- 4. Eseguiamo la prima volta per preparare i dati già esistenti
SELECT refresh_routing_topology();
