# Database Schema — Single Source of Truth

> This file defines the database schema for the project.
> ZooCode uses this as the authoritative reference when generating or modifying database-related code.
> **Edit this file to reflect schema changes** — the AI agent will update code accordingly.

**Technology:** .NET 10 Fullstack (ASP.NET Core, PostgreSQL, EF Core)
**Last Updated:** 2026-06-22 18:15

---

## Entities

### AppUser

Represents a registered user of the blog platform. The root superuser is seeded and has no group assignment.

#### Fields

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `Id` | `Guid` | No | Primary key |
| `UserName` | `string` | No | Unique username for login |
| `Email` | `string` | Yes | User email address |
| `PasswordHash` | `string` | No | Hashed password |
| `IsRoot` | `bool` | No | True only for the root superuser account; root is not assigned to any group |
| `CreatedAt` | `DateTime` | No | Account creation timestamp |
| `UpdatedAt` | `DateTime` | Yes | Last update timestamp |

#### Relationships

- **Many To Many** `Group`: A user can belong to multiple groups (Author, Admin, Viewer). Root has no group.
- **One To Many** `Post`: A user authors multiple blog posts
- **One To Many** `Comment`: A user writes multiple comments

---

### Group

Represents a user role/group: Author, Admin, Viewer, or Public.

#### Fields

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `Id` | `Guid` | No | Primary key |
| `Name` | `string` | No | Group name (Author, Admin, Viewer, Public) |
| `Description` | `string` | Yes | Optional description of the group's permissions |

#### Relationships

- **Many To Many** `AppUser`: Multiple users can belong to a group

---

### Post

Represents a blog post written by an author. Contains markdown content, supports embedded images and social media/video links.

#### Fields

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `Id` | `Guid` | No | Primary key |
| `Title` | `string` | No | Post title |
| `Content` | `string` | No | Post body in markdown format |
| `AuthorId` | `Guid` | No | FK to AppUser who authored the post |
| `IsPublished` | `bool` | No | Whether the post is publicly visible |
| `CreatedAt` | `DateTime` | No | Creation timestamp |
| `UpdatedAt` | `DateTime` | Yes | Last edit timestamp |
| `PublishedAt` | `DateTime` | Yes | Timestamp when post was published |

#### Relationships

- **Many To One** `AppUser`: Each post belongs to one author
- **One To Many** `Comment`: A post has many comments
- **One To Many** `Media`: A post can have multiple uploaded images stored in DB
- **One To Many** `PostRating`: A post has many ratings (likes/dislikes)
- **One To Many** `Bookmark`: A post can be bookmarked by many users

---

### PostRating

Represents a like or dislike rating on a blog post by an authenticated user. One rating per user per post (unique constraint).

#### Fields

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `Id` | `Guid` | No | Primary key |
| `PostId` | `Guid` | No | FK to Post being rated |
| `UserId` | `Guid` | No | FK to AppUser who rated |
| `IsLike` | `bool` | No | True = Like, False = Dislike |
| `CreatedAt` | `DateTime` | No | Rating creation timestamp |

#### Relationships

- **Many To One** `Post`: Each rating belongs to one post
- **Many To One** `AppUser`: Each rating belongs to one user

---

### Bookmark

Represents a bookmark/saved post by an authenticated user. One bookmark per user per post (unique constraint).

#### Fields

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `Id` | `Guid` | No | Primary key |
| `PostId` | `Guid` | No | FK to Post being bookmarked |
| `UserId` | `Guid` | No | FK to AppUser who bookmarked |
| `CreatedAt` | `DateTime` | No | Bookmark creation timestamp |

#### Relationships

- **Many To One** `Post`: Each bookmark belongs to one post
- **Many To One** `AppUser`: Each bookmark belongs to one user

---

### Comment

Represents a comment on a blog post. Supports nesting via ParentCommentId. Guest comments require moderation approval.

#### Fields

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `Id` | `Guid` | No | Primary key |
| `Content` | `string` | No | Comment text |
| `PostId` | `Guid` | No | FK to Post being commented on |
| `UserId` | `Guid` | Yes | FK to AppUser if commenter is authenticated; null for guests |
| `ParentCommentId` | `Guid` | Yes | FK to parent Comment for nested replies; null for top-level comments |
| `GuestName` | `string` | Yes | Name provided by guest commenter (when UserId is null) |
| `GuestEmail` | `string` | Yes | Email provided by guest commenter (when UserId is null) |
| `IsApproved` | `bool` | No | True if approved/visible. Authenticated users' comments are auto-approved; guest comments start as false. |
| `CreatedAt` | `DateTime` | No | Comment creation timestamp |

#### Relationships

- **Many To One** `Post`: Each comment belongs to one post
- **Many To One** `AppUser`: Comment author if authenticated; null for guests
- **Many To One** `Comment`: Parent comment for nested replies
- **One To Many** `Comment`: Replies to this comment (nested children)

---

### Media

Represents an uploaded image embedded in a blog post. Stored as binary data in the database.

#### Fields

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `Id` | `Guid` | No | Primary key |
| `PostId` | `Guid` | No | FK to Post this media belongs to |
| `FileName` | `string` | No | Original file name |
| `ContentType` | `string` | No | MIME type of the file |
| `Data` | `byte[]` | No | Binary content of the image stored in DB |
| `CreatedAt` | `DateTime` | No | Upload timestamp |

#### Relationships

- **Many To One** `Post`: Each media item belongs to one post

---

### SystemSetting

Key-value store for blog-wide configuration such as blog title, moderation toggle, and other admin-configurable settings.

#### Fields

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `Id` | `Guid` | No | Primary key |
| `Key` | `string` | No | Setting key (e.g., BlogTitle, ModerationEnabled) |
| `Value` | `string` | Yes | Setting value as string |
| `UpdatedAt` | `DateTime` | Yes | Last modification timestamp |

---

## Indexes

- `unnamed`: Unique index on username for fast login lookup
- `unnamed`: Unique index on group name
- `unnamed`: Index for chronological post listing
- `unnamed`: Index for querying posts by author
- `unnamed`: Composite index for fetching approved comments per post
- `unnamed`: Index for moderation queue queries
- `unnamed`: Index for nested comment retrieval
- `unnamed`: Index for retrieving media by post
- `unnamed`: Unique index on setting key
- `unnamed`: Unique composite index on (PostId, UserId) for PostRating
- `unnamed`: Index on PostId for PostRating count queries
- `unnamed`: Unique composite index on (PostId, UserId) for Bookmark
- `unnamed`: Index on UserId for Bookmark user queries

## Additional Notes

The root superuser is seeded on first launch with credentials from appsettings.json (Blog section). Root is not assigned to any Group but has unrestricted permissions. Guest comments (UserId is null) require manual approval by Admin or root before becoming publicly visible. Authenticated users' comments are auto-approved. Images are stored as binary in the Media table, not on the filesystem. The database auto-migrates on first launch. PostgreSQL runs in Docker on host port 5433 mapped to container port 5432. PostRating enforces one vote per user per post (unique constraint on PostId+UserId). Bookmark enforces one bookmark per user per post (unique constraint on PostId+UserId).

The root superuser is seeded on first launch with credentials from appsettings.json (Blog section). Root is not assigned to any Group but has unrestricted permissions. Guest comments (UserId is null) require manual approval by Admin or root before becoming publicly visible. Authenticated users' comments are auto-approved. Images are stored as binary in the Media table, not on the filesystem. The database auto-migrates on first launch. PostgreSQL runs in Docker on host port 5433 mapped to container port 5432.

---

## How to Modify This Schema

1. Edit the entity definitions above (add/remove/modify fields, relationships, etc.)
2. ZooCode will detect changes to this file and update:
   - Database migration files
   - Model/entity classes
   - API endpoints that interact with changed entities
   - Repository/data access layer code
