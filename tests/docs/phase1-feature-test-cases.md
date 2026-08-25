# Phase 1 Feature Test Cases (F01–F06)

| ID | Feature | Requirement | Priority |
|----|---------|-------------|----------|
| TC_F_INBOX_001 | F01 | Push + feed returns item and unread count | High |
| TC_F_INBOX_002 | F01 | Mark read + archive filters feed | High |
| TC_ERR_INBOX_003 | F01 | Mark read wrong user fails | High |
| TC_F_INBOX_004 | F01 | Mark all read | Medium |
| TC_F_DIGEST_001 | F02 | Save policy + buffer rows | High |
| TC_F_DIGEST_002 | F02 | Flush marks due rows | High |
| TC_E_DIGEST_003 | F02 | Fresh rows not flushed | Medium |
| TC_F_TH_001 | F03 | Allow until max then block | High |
| TC_F_TH_002 | F03 | Channel-scoped policies | High |
| TC_F_TH_003 | F03 | No policy always allow | Medium |
| TC_F_TOPIC_001 | F04 | Topic + subscribers | High |
| TC_F_TOPIC_002 | F04 | Unsubscribe | High |
| TC_E_TOPIC_003 | F04 | Double subscribe idempotent | Medium |
| TC_F_DEV_001 | F05 | Register + list devices | High |
| TC_ERR_DEV_002 | F05 | Invalid platform rejected | High |
| TC_F_DEV_003 | F05 | Unregister | High |
| TC_E_DEV_004 | F05 | Re-register reactivates | Medium |
| TC_F_ACT_001 | F06 | Activity includes notifications | High |
