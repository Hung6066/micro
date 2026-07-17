#!/bin/sh
psql -U postgres -d patientdb -c "SELECT COUNT(*) FROM \"Patients\";"
psql -U postgres -d appointmentdb -c "SELECT COUNT(*) FROM \"Appointments\";"
psql -U postgres -d clinicaldb -c "SELECT COUNT(*) FROM \"Encounters\";"
psql -U postgres -d pharmacydb -c "SELECT COUNT(*) FROM \"Medications\";"
psql -U postgres -d labdb -c "SELECT COUNT(*) FROM \"LabOrders\";"
psql -U postgres -d billingdb -c "SELECT COUNT(*) FROM \"Invoices\";"
