#!/bin/bash
set -euo pipefail

echo "=== Building with Bazel ==="

# Build all services
bazel build //bazel/src/Services/PatientService:PatientService.Api
bazel build //bazel/src/Services/IdentityService:IdentityService.Api
bazel build //bazel/src/Services/AppointmentService:AppointmentService.Api
bazel build //bazel/src/Services/ClinicalService:ClinicalService.Api

# Build Docker images
bazel build //bazel/src/Services/PatientService:patient_service_image

# Run all tests
bazel test //...

# Generate code coverage
bazel coverage //...

echo "=== Build Complete ==="
