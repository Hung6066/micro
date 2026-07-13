-- Create databases for each service
CREATE DATABASE IF NOT EXISTS patientdb;
CREATE DATABASE IF NOT EXISTS identitydb;
CREATE DATABASE IF NOT EXISTS appointmentdb;
CREATE DATABASE IF NOT EXISTS clinicaldb;

-- Create users with passwords
CREATE USER IF NOT EXISTS patient_user WITH PASSWORD '${PATIENT_DB_PASSWORD}';
CREATE USER IF NOT EXISTS identity_user WITH PASSWORD '${IDENTITY_DB_PASSWORD}';
CREATE USER IF NOT EXISTS appointment_user WITH PASSWORD '${APPOINTMENT_DB_PASSWORD}';
CREATE USER IF NOT EXISTS clinical_user WITH PASSWORD '${CLINICAL_DB_PASSWORD}';

-- Grant privileges
GRANT ALL ON DATABASE patientdb TO patient_user;
GRANT ALL ON DATABASE identitydb TO identity_user;
GRANT ALL ON DATABASE appointmentdb TO appointment_user;
GRANT ALL ON DATABASE clinicaldb TO clinical_user;

-- Set zone configs for multi-region
ALTER DATABASE patientdb CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';
ALTER DATABASE identitydb CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';
ALTER DATABASE appointmentdb CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';
ALTER DATABASE clinicaldb CONFIGURE ZONE USING constraints = '{"us-east1": 2, "europe-west1": 1, "asia-east1": 1}';

-- Global tables (replicated everywhere)
ALTER TABLE system.users CONFIGURE ZONE USING num_replicas = 5, constraints = '{"+us-east1=2,+europe-west1=2,+asia-east1=1}';
