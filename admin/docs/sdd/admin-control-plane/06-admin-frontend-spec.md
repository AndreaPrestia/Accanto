# 06 — Admin Frontend Spec

## Goal

Creare un frontend Admin separato per operazioni tecniche minime.

Il frontend deve essere separato dalla PWA pubblica.

Path suggerito:

```text
admin/accanto-admin-web
```

## Stack

- React;
- TypeScript;
- Vite;
- Tailwind CSS;
- React Router;
- TanStack Query o fetch wrapper semplice;
- form validation leggera.

## Routes

```text
/admin/login
/admin/dashboard
/admin/users
/admin/users/:id
/admin/audit-logs
/admin/operations
/admin/system
```

## Layout

Desktop-first, responsive.

Struttura:

```text
Sidebar:
- Dashboard
- Users
- Audit logs
- Operations
- System

Header:
- Admin email
- Role
- Logout
```

## Tone

Il pannello admin deve essere tecnico e asciutto.

Non usare linguaggio emotivo della PWA pubblica.  
L’admin non è un prodotto per caregiver, è uno strumento operativo.

## Login page

Fields:

- email;
- password.

Errors:

- credenziali non valide;
- account admin disattivato;
- rate limited.

## Dashboard

Cards:

- total users;
- disabled users;
- total storage used;
- recent operations;
- recent technical warnings;
- public API internal health;
- admin DB health.

No analytics invasive.

## Users list

Columns:

```text
Email
Display name
Created at
Last login
Status
Care circles count
Documents count
Storage used
Actions
```

Forbidden columns:

```text
Care circle names
Original filenames
Timeline titles
Timeline content
Doctor questions
Shared updates
```

Filters:

- search email/display name;
- status active/disabled;
- created date optional;
- page/page size.

## User detail

Show only:

```text
Email
Display name
UserId
Created at
Last login
Status
Care circle count
Document count
Storage used
Timeline entry count
Disabled at
Disabled reason
```

Actions:

```text
Disable account
Enable account
Revoke sessions
Start data deletion
```

## Action modals

Every mutating action must open a confirmation modal.

Required fields:

- reason textarea;
- explicit confirmation button.

Example copy:

```text
This action will be recorded in the admin audit log.
It will not read or modify the user's private content.
```

Validation:

- reason required;
- min length e.g. 10 chars;
- max length e.g. 500 chars.

## Audit logs page

Columns:

```text
Created at
Admin
Action
Target type
Target id
Reason
IP
User agent
```

Filters:

- action;
- admin;
- target type;
- date range.

## Operations page

Columns:

```text
Created at
Operation type
Target user
Status
Reason
Completed at
Error
```

## System page

Cards:

- Admin API health;
- Admin DB health;
- Public/Internal API health;
- last checked;
- technical logs.

Technical logs must not show payloads.

## Client auth handling

- store access token in memory if possible;
- refresh token handling according to backend design;
- logout clears tokens;
- redirect unauthenticated users to `/admin/login`;
- 401 triggers logout or refresh.

## UI privacy checks

The frontend must not render or request:

- timeline content;
- document content;
- original filenames;
- care circle names;
- doctor questions;
- shared updates.

If API accidentally returns forbidden fields, the UI must still not display them, but backend tests must prevent this situation.
