-- Índices para las consultas del catálogo filtradas por usuario.
-- Ejecutar una vez en la base de datos PostgreSQL existente.
CREATE INDEX IF NOT EXISTS ix_componente_id_usuario
    ON componente (id_usuario);

CREATE INDEX IF NOT EXISTS ix_juegos_id_usuario
    ON juegos (id_usuario);

BEGIN;

-- Conserva el ID mas antiguo y combina el estado de las filas duplicadas.
WITH grouped_devices AS (
    SELECT
        id_dispositivo,
        FIRST_VALUE(id_dispositivo) OVER product_window AS keep_id,
        COUNT(*) OVER product_window AS product_count,
        BOOL_OR(estado_coleccion = 'COLECCION' OR LOWER(COALESCE(estado, '')) = 'adquirido')
            OVER product_window AS was_acquired,
        MAX(fecha_compra) OVER product_window AS latest_purchase_date,
        FIRST_VALUE(imagen_url) OVER image_window AS preferred_image
    FROM dispositivo
        WHERE NULLIF(BTRIM(marca), '') IS NOT NULL
            AND NULLIF(BTRIM(modelo), '') IS NOT NULL
    WINDOW
        product_window AS (
            PARTITION BY
                id_usuario,
                id_categoria,
                COALESCE(LOWER(BTRIM(marca)), ''),
                COALESCE(LOWER(BTRIM(modelo)), '')
            ORDER BY id_dispositivo
            ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING
        ),
        image_window AS (
            PARTITION BY
                id_usuario,
                id_categoria,
                COALESCE(LOWER(BTRIM(marca)), ''),
                COALESCE(LOWER(BTRIM(modelo)), '')
            ORDER BY (imagen_url IS NULL), id_dispositivo
            ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING
        )
)
UPDATE dispositivo AS device
SET estado_coleccion = CASE WHEN grouped.was_acquired THEN 'COLECCION' ELSE 'DESEADO' END,
    estado = CASE WHEN grouped.was_acquired THEN 'Adquirido' ELSE device.estado END,
    fecha_compra = CASE
        WHEN grouped.was_acquired THEN COALESCE(grouped.latest_purchase_date, CURRENT_DATE)
        ELSE NULL
    END,
    imagen_url = COALESCE(device.imagen_url, grouped.preferred_image)
FROM grouped_devices AS grouped
WHERE device.id_dispositivo = grouped.keep_id
  AND grouped.product_count > 1;

WITH ranked_devices AS (
    SELECT
        id_dispositivo,
        ROW_NUMBER() OVER (
            PARTITION BY
                id_usuario,
                id_categoria,
                COALESCE(LOWER(BTRIM(marca)), ''),
                COALESCE(LOWER(BTRIM(modelo)), '')
            ORDER BY id_dispositivo
        ) AS duplicate_number
    FROM dispositivo
        WHERE NULLIF(BTRIM(marca), '') IS NOT NULL
            AND NULLIF(BTRIM(modelo), '') IS NOT NULL
)
DELETE FROM dispositivo AS device
USING ranked_devices AS ranked
WHERE device.id_dispositivo = ranked.id_dispositivo
  AND ranked.duplicate_number > 1;

SELECT setval(
        pg_get_serial_sequence('dispositivo', 'id_dispositivo'),
        COALESCE(MAX(id_dispositivo), 1),
        COUNT(*) > 0
)
FROM dispositivo;

-- Un dispositivo con la misma categoría, marca y modelo solo puede aparecer una vez por usuario.
CREATE UNIQUE INDEX IF NOT EXISTS ux_dispositivo_usuario_producto
    ON dispositivo (
        id_usuario,
        id_categoria,
        COALESCE(LOWER(BTRIM(marca)), ''),
        COALESCE(LOWER(BTRIM(modelo)), '')
        )
        WHERE NULLIF(BTRIM(marca), '') IS NOT NULL
            AND NULLIF(BTRIM(modelo), '') IS NOT NULL;

COMMIT;
