-- The administrator provisions roles. Only the migrator creates application objects.
\set VERBOSITY terse
SET log_statement = 'none';
SET log_min_error_statement = 'panic';
SELECT pg_advisory_lock(68310002);
SELECT NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'identity_migrator') AS need_migrator,
       NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'identity_api') AS need_api,
       NOT EXISTS (SELECT FROM pg_database WHERE datname = 'shipwise_identity') AS need_db
\gset
\if :need_migrator
    \set migrator_password `cat /run/secrets/identity-migrator-password`
    CREATE ROLE identity_migrator LOGIN PASSWORD :'migrator_password';
    \unset migrator_password
\endif
\if :need_api
    \set api_password `cat /run/secrets/identity-api-password`
    CREATE ROLE identity_api LOGIN PASSWORD :'api_password';
    \unset api_password
\endif
\if :need_db
    CREATE DATABASE shipwise_identity OWNER identity_migrator;
\endif

\connect shipwise_identity
BEGIN;
SELECT pg_advisory_xact_lock(68310002);
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON DATABASE shipwise_identity FROM PUBLIC;
GRANT CONNECT ON DATABASE shipwise_identity TO identity_api;
ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator GRANT USAGE ON SCHEMAS TO identity_api;
ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO identity_api;
ALTER DEFAULT PRIVILEGES FOR ROLE identity_migrator GRANT USAGE, SELECT ON SEQUENCES TO identity_api;
COMMIT;
