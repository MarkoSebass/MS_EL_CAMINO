-- Índices para las consultas del catálogo filtradas por usuario.
-- Ejecutar una vez en la base de datos PostgreSQL existente.
CREATE INDEX IF NOT EXISTS ix_componente_id_usuario
    ON componente (id_usuario);

CREATE INDEX IF NOT EXISTS ix_juegos_id_usuario
    ON juegos (id_usuario);
