SELECT format('CREATE DATABASE %I OWNER his_hope', dbname)
FROM unnest(ARRAY['identitydb','appointmentdb','clinicaldb','billingdb','labdb','pharmacydb','unleashdb']) AS dbname
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = dbname)
\gexec
