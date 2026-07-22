### Task 3: Dashboard Rate Limiting

**Files:**
- Modify `src/Bff/SystemDashboard.Bff/Program.cs` — add `builder.Services.AddHisHopeRateLimiting(builder.Configuration)` and `app.UseRateLimiting()`
- Modify `src/Bff/SystemDashboard.Bff/appsettings.json` — add RateLimiting section

```json
"RateLimiting": {
  "MaxRequestsPerIp": 100,
  "MaxRequestsPerUser": 200,
  "WindowSeconds": 60
}
```

- [ ] Modify 2 files, build, commit: `feat(dashboard): add rate limiting to dashboard BFF`

---

