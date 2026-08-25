# Phase 2 (F07–F14)

| ID | Feature | Notes |
|----|---------|-------|
| F07 | Workflow DSL | `GET .../export`, `POST .../import`, validates step types including `http` |
| F08/F09 | Layouts + partials | `{{content}}`, `{{>partial}}`, render API |
| F10 | Preference embed | `GET /api/v1/preferences/{userId}/embed` |
| F11 | Weekly schedule + critical | `WeeklySchedule` on preference; `Priority.Critical` bypasses quiet/schedule |
| F12 | CollapseKey | 24h dedup per recipient on Accept |
| F13 | Cross-channel read | Sync on engagement open + explicit API |
| F14 | HTTP workflow step | `HttpStepHandler`, client `workflow-http` |
