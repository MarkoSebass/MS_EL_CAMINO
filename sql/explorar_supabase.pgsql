-- Exploración inicial de Supabase/PostgreSQL (solo lectura).
-- Conecta primero desde el explorador PostgreSQL y ejecuta con F5.

-- 1. Tablas y vistas del esquema de la aplicación.
SELECT table_schema, table_name, table_type
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_type IN ('BASE TABLE', 'VIEW')
ORDER BY table_name;

-- 2. Columnas de las tablas públicas.
SELECT table_name, ordinal_position, column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public'
ORDER BY table_name, ordinal_position;

-- 3. Plantilla: reemplaza nombre_tabla por una tabla del esquema public.
-- SELECT * FROM public.nombre_tabla LIMIT 100;

-- 4. Confirma servidor, base de datos y usuario de la conexión.
SELECT current_database() AS database_name,
       current_user AS user_name,
       inet_server_addr() AS server_address,
       now() AS connected_at;