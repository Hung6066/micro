# His.Hope Git Hooks

Install once per checkout:

```powershell
npm run hooks:install
```

The hooks run the policy gate independently of the OpenCode agent selected:

- `pre-commit`: validates staged frontend/backend changes.
- `pre-push`: validates the current worktree scope.
- CI `His.Hope Policy Gate`: runs the full backend and frontend gate and must be a required branch-protection check.

Hooks can technically be bypassed with `--no-verify`; the required CI check is the non-bypassable merge control.
