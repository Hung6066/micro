### Task 5: NuGet Cache PVC + NuGet.config

**Files:**
- Create `cicd/tekton/volumes/nuget-cache-pvc.yaml` — PVC 10Gi ReadWriteMany
- Modify `cicd/tekton/tasks/dotnet-build.yaml` — change nuget-cache volume from `emptyDir` to `persistentVolumeClaim: claimName: nuget-cache-pvc`
- Create `NuGet.config` at repo root — nuget.org source

- [ ] Create 2 new + modify 1, commit: `feat(ci): add persistent NuGet cache PVC and NuGet.config`

---

