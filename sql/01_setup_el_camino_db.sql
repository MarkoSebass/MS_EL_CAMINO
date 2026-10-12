-- ============================================================================
-- SCRIPT DE BASE DE DATOS: PROYECTO "EL CAMINO"
-- Motor: PostgreSQL
-- Herramienta recomendada: DBeaver
-- Propósito: Creación de la tabla USUARIO y registro semilla de administrador.
-- ============================================================================

-- 1. Crear la tabla de usuarios conforme a la estructura de 'El Camino'
-- Si la tabla ya existe, no causará conflicto (IF NOT EXISTS).
CREATE TABLE IF NOT EXISTS usuario (
    id_usuario SERIAL PRIMARY KEY,
    nombres VARCHAR(100) NOT NULL,
    apellidos VARCHAR(100) NOT NULL,
    usu_lg VARCHAR(100) NOT NULL UNIQUE,
    correo VARCHAR(150) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    rol VARCHAR(50) NOT NULL DEFAULT 'USER',
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_registro TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);

ALTER TABLE usuario
ADD COLUMN IF NOT EXISTS rol VARCHAR(50) NOT NULL DEFAULT 'USER';

-- 2. Índice único para acelerar y asegurar la búsqueda por correo en minúsculas
CREATE UNIQUE INDEX IF NOT EXISTS ix_usuario_usu_lg
ON usuario (LOWER(usu_lg));

CREATE UNIQUE INDEX IF NOT EXISTS ix_usuario_correo 
ON usuario (LOWER(correo));

-- 3. Limpieza opcional del usuario de prueba antes de insertar (para evitar duplicados)
DELETE FROM usuario
WHERE LOWER(correo) = 'admin@elcamino.com' OR LOWER(usu_lg) = 'admin';

-- 4. Inserción de usuario administrador para pruebas
-- Credenciales de acceso:
-- Correo: admin@elcamino.com
-- Contraseña en texto plano: 123456
-- Hash generado con BCrypt (Factor de costo 11): $2a$11$HpSpO4q.n9Sh9e9WFPJPJudMhRXN2ShA6DcdV9rQshTPLw6yhn09i
INSERT INTO usuario (
    nombres, 
    apellidos, 
    usu_lg,
    correo, 
    password_hash, 
    rol,
    activo, 
    fecha_registro
) 
VALUES (
    'Marko', 
    'Camino', 
    'admin',
    'admin@elcamino.com', 
    '$2a$11$HpSpO4q.n9Sh9e9WFPJPJudMhRXN2ShA6DcdV9rQshTPLw6yhn09i', 
    'ADMIN',
    TRUE, 
    NOW()
);

-- 5. Consulta de verificación
SELECT 
    id_usuario, 
    usu_lg,
    nombres, 
    apellidos, 
    correo, 
    rol,
    activo, 
    fecha_registro 
FROM usuario;
