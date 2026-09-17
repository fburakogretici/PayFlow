-- PayFlow Database-per-Service Initialization Script
-- Her mikroservisin veri tabanı tamamen izoledir.

CREATE DATABASE payflow_catalog_db;
CREATE DATABASE payflow_ordering_db;
CREATE DATABASE payflow_payment_db;

\connect payflow_catalog_db;
GRANT ALL PRIVILEGES ON DATABASE payflow_catalog_db TO postgres;

\connect payflow_ordering_db;
GRANT ALL PRIVILEGES ON DATABASE payflow_ordering_db TO postgres;

\connect payflow_payment_db;
GRANT ALL PRIVILEGES ON DATABASE payflow_payment_db TO postgres;
