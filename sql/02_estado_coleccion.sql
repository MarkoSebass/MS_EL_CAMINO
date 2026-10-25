-- Clasifica cada dispositivo dentro de la colección actual o futura.
ALTER TABLE dispositivo
    ADD COLUMN IF NOT EXISTS estado_coleccion VARCHAR(20) NOT NULL DEFAULT 'COLECCION';

UPDATE dispositivo
SET estado_coleccion = 'COLECCION'
WHERE estado_coleccion IS NULL
   OR UPPER(estado_coleccion) NOT IN ('COLECCION', 'DESEADO');

UPDATE dispositivo
SET estado_coleccion = UPPER(estado_coleccion)
WHERE estado_coleccion IS NOT NULL
  AND UPPER(estado_coleccion) IN ('COLECCION', 'DESEADO');

ALTER TABLE dispositivo
    ALTER COLUMN estado_coleccion SET DEFAULT 'COLECCION',
    ALTER COLUMN estado_coleccion SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_dispositivo_estado_coleccion'
          AND conrelid = 'dispositivo'::regclass
    ) THEN
        ALTER TABLE dispositivo
            ADD CONSTRAINT ck_dispositivo_estado_coleccion
            CHECK (estado_coleccion IN ('COLECCION', 'DESEADO'));
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_dispositivo_usuario_estado_coleccion
    ON dispositivo (id_usuario, estado_coleccion);
