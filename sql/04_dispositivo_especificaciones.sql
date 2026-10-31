-- Guarda especificaciones dinámicas del dispositivo como JSON serializado.
-- Texto JSON para permitir agregar campos por categoría sin modificar el esquema.
ALTER TABLE IF EXISTS dispositivo
    ADD COLUMN IF NOT EXISTS especificaciones TEXT;
