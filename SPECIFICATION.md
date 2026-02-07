# Kudos System Specification

## Functional Requirements

### User Stories

1. As a user, I can select another user from a list of colleagues.
2. As a user, I can write a message of appreciation (max 240 characters).
3. As a user, I can submit the kudos which gets stored in the database.
4. As a user, I can view a feed of recent kudos on the dashboard.
5. As an administrator, I can hide or delete inappropriate kudos messages.

### Acceptance Criteria

- Authenticated users can view the kudos form and feed.
- Users can pick a colleague from the list and submit a message up to 240 characters.
- Submitted kudos appear in the public feed ordered by newest first.
- The feed supports pagination and optional filters (team, search).
- Administrators can mark a kudos as hidden (soft hide) or delete it.
- Hidden kudos do not appear in the public feed for non-admin users.
- Input validation errors return clear error messages.
- The UI is usable on mobile and desktop breakpoints.
- Edge cases handled:
  - Spam or duplicate submissions can be hidden by an admin.
  - Inappropriate content can be hidden or deleted by an admin.
  - Deleted kudos are removed from the feed entirely.

## Technical Design

### Database Schema

**users**
- `_id` (ObjectId)
- `name` (string)
- `team` (string)
- `externalId` (string, unique per IdP user)

**kudos**
- `_id` (ObjectId)
- `toUserId` (ObjectId)
- `toUserName` (string)
- `toUserTeam` (string)
- `fromUserId` (ObjectId)
- `fromUserName` (string)
- `fromUserTeam` (string)
- `message` (string)
- `createdAt` (datetime)
- `isVisible` (boolean, default true)
- `moderatedBy` (string, nullable)
- `moderatedAt` (datetime, nullable)
- `moderationReason` (string, nullable)

### API Endpoints

- `GET /api/users`
  - Auth required
  - Returns list of users

- `GET /api/kudos?page=&pageSize=&team=&search=`
  - Auth required
  - Returns paginated kudos feed
  - Non-admin users only see `isVisible=true`

- `POST /api/kudos`
  - Auth required
  - Creates kudos

- `PATCH /api/kudos/{id}/visibility`
  - Admin only
  - Body: `{ "isVisible": true|false, "reason": "string" }`
  - Sets visibility and moderation metadata

- `DELETE /api/kudos/{id}`
  - Admin only
  - Hard delete

### Frontend Components

- `App`
  - Auth wrapper (MSAL)
  - Kudos form
  - Feed with pagination and filters
  - Admin moderation controls in feed cards

### Security Considerations

- AWS Cognito OAuth2/OIDC JWT validation
- Role-based authorization for admin moderation endpoints
- Input validation for message length and required fields
- Server-side filtering to prevent hidden content exposure

### Performance Considerations

- Pagination (page, pageSize)
- Indexed fields on `createdAt`, `toUserTeam`, `isVisible`

### Error Handling and Logging

- Return structured error responses for validation failures
- Log moderation actions with `moderatedBy`, `moderatedAt`, `moderationReason`

## Implementation Plan

1. Define moderation fields on kudos model and update persistence.
2. Add admin-only moderation endpoints (hide/show, delete).
3. Update feed queries to exclude hidden kudos for non-admin users.
4. Update frontend UI for admin moderation controls.
5. Add tests for visibility and admin moderation behavior.
6. Validate responsive UI and error handling.
