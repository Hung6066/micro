[CmdletBinding()]
param(
    [string]$Namespace = "linkerd-cni",
    [string]$DaemonSet = "linkerd-cni"
)

$ErrorActionPreference = "Stop"

# K3s keeps its CNI directories below /var/lib/rancher/k3s. The stock
# Linkerd CNI manifest targets /etc/cni/net.d and /opt/cni/bin, which is valid
# for a conventional host but silently bypasses K3s in a k3d/K3s node.
$configPatch = '[{"op":"replace","path":"/data/dest_cni_bin_dir","value":"/var/lib/rancher/k3s/data/cni"},{"op":"replace","path":"/data/dest_cni_net_dir","value":"/var/lib/rancher/k3s/agent/etc/cni/net.d"}]'
$daemonSetPatch = '[{"op":"replace","path":"/spec/template/spec/volumes/0/hostPath/path","value":"/var/lib/rancher/k3s/data/cni"},{"op":"replace","path":"/spec/template/spec/volumes/1/hostPath/path","value":"/var/lib/rancher/k3s/agent/etc/cni/net.d"},{"op":"replace","path":"/spec/template/spec/containers/0/volumeMounts/0/mountPath","value":"/host/var/lib/rancher/k3s/data/cni"},{"op":"replace","path":"/spec/template/spec/containers/0/volumeMounts/1/mountPath","value":"/host/var/lib/rancher/k3s/agent/etc/cni/net.d"},{"op":"add","path":"/spec/template/spec/containers/0/securityContext/privileged","value":true},{"op":"add","path":"/spec/template/spec/containers/0/securityContext/runAsUser","value":0},{"op":"add","path":"/spec/template/spec/containers/0/securityContext/runAsGroup","value":0},{"op":"add","path":"/spec/template/spec/containers/0/securityContext/runAsNonRoot","value":false},{"op":"add","path":"/spec/template/spec/securityContext/runAsNonRoot","value":false}]'

kubectl patch configmap "$($DaemonSet)-config" -n $Namespace --type=json -p $configPatch | Out-Null
kubectl patch daemonset $DaemonSet -n $Namespace --type=json -p $daemonSetPatch | Out-Null
kubectl rollout status daemonset/$DaemonSet -n $Namespace --timeout=180s

$pods = @(kubectl get pods -n $Namespace -l k8s-app=$DaemonSet -o json | ConvertFrom-Json).items
if ($pods.Count -eq 0 -or @($pods | Where-Object { $_.status.containerStatuses[0].ready }).Count -ne $pods.Count) {
    throw "Linkerd CNI is not Ready on every K3s node"
}
Write-Host "Linkerd CNI K3s paths: PASS ($($pods.Count) nodes)"
