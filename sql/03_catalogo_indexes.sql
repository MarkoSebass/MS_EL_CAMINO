-- Índices para las consultas del catálogo filtradas por usuario.
-- Ejecutar una vez en la base de datos PostgreSQL existente.
CREATE INDEX IF NOT EXISTS ix_componente_id_usuario
    ON componente (id_usuario);

CREATE INDEX IF NOT EXISTS ix_juegos_id_usuario
    ON juegos (id_usuario);

-- Un dispositivo con la misma categoría, marca y modelo solo puede aparecer una vez por usuario.
CREATE UNIQUE INDEX IF NOT EXISTS ux_dispositivo_usuario_producto
    ON dispositivo (
        id_usuario,
        id_categoria,
        COALESCE(LOWER(BTRIM(marca)), ''),
        COALESCE(LOWER(BTRIM(modelo)), '')
    );
