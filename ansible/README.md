Cách dùng:
# Local dev
ansible-playbook -i inventory/dev.yml site.yml -t docker-compose

# Staging K3s (tự động cài K3s + deploy)
ansible-playbook -i inventory/staging.yml site.yml -t k3s

# Production GKE
ansible-playbook -i inventory/prod.yml site.yml -t gke

# Canary deploy 1 service
ansible-playbook -i inventory/prod.yml site.yml -t canary \
  --extra-vars "service=patientservice version=v2.1.0"

# Rollback
ansible-playbook -i inventory/prod.yml site.yml -t rollback \
  --extra-vars "service=patientservice"

# Health check toàn bộ hệ thống
ansible-playbook -i inventory/prod.yml site.yml -t health-check