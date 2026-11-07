-- The search database: one schema per service.
-- Each service has a role that can write only to its own schema.
-- The search role can read from all schemas and write to none.

\connect search

REVOKE CREATE ON SCHEMA public FROM PUBLIC;

CREATE ROLE registry LOGIN PASSWORD 'registry';
CREATE ROLE reservations LOGIN PASSWORD 'reservations';
CREATE ROLE marketing LOGIN PASSWORD 'marketing';
CREATE ROLE sales LOGIN PASSWORD 'sales';
CREATE ROLE search LOGIN PASSWORD 'search';

CREATE SCHEMA registry AUTHORIZATION registry;
CREATE SCHEMA reservations AUTHORIZATION reservations;
CREATE SCHEMA marketing AUTHORIZATION marketing;
CREATE SCHEMA sales AUTHORIZATION sales;

-- Search reads everything, including tables services create later.
DO $$
DECLARE
    owner text;
BEGIN
    FOREACH owner IN ARRAY ARRAY['registry', 'reservations', 'marketing', 'sales']
    LOOP
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO search', owner);
        EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT ON TABLES TO search', owner, owner);
    END LOOP;
END
$$;
