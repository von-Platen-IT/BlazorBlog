# UI Specification — Single Source of Truth

> This file defines the user interface specification for the project.
> ZooCode uses this as the authoritative reference when generating or modifying UI code.
> **Edit this file to reflect UI changes** — the AI agent will update code accordingly.

**Technology:** .NET 10 Fullstack (ASP.NET Core, PostgreSQL, EF Core)
**Last Updated:** 2026-06-21 12:38

---

## Pages / Views

### Home / Post List

**Route:** `/`

Chronological list of all published blog posts visible to all visitors (including unauthenticated). Shows post title, author, date, and excerpt.

#### Components

| Component | Type | Description |
|-----------|------|-------------|
| `PostList` | `list` | Chronological list of published posts with title, author, date, excerpt |
| `Pagination` | `navigation` | Pagination controls for browsing posts |
| `TopNav` | `navigation` | Top navigation bar with login/register or user menu |

#### User Actions

- Click post to view detail
- Navigate pages
- Login/Register from nav

---

### Post Detail

**Route:** `/posts/:id`

Full blog post view with rendered markdown content, embedded images, social media/video embeds, and the nested comment section.

#### Components

| Component | Type | Description |
|-----------|------|-------------|
| `PostContent` | `detail` | Renders full post with markdown, images, and embedded social/video links |
| `CommentSection` | `list` | Nested comment tree with reply functionality |
| `CommentForm` | `form` | Form to add a comment (authenticated or guest with name/email) |
| `ReplyForm` | `form` | Inline reply form for nested comments |

#### User Actions

- Read post
- Add comment
- Reply to comment
- View nested replies

---

### Login

**Route:** `/login`

Login page for registered users.

#### Components

| Component | Type | Description |
|-----------|------|-------------|
| `LoginForm` | `form` | Username and password fields with submit button |

#### User Actions

- Enter credentials
- Submit login

---

### Register

**Route:** `/register`

Registration page for new users.

#### Components

| Component | Type | Description |
|-----------|------|-------------|
| `RegisterForm` | `form` | Username, email, and password fields for new account creation |

#### User Actions

- Fill registration form
- Submit registration

---

### Post Editor

**Route:** `/posts/new`

Markdown editor for authors to create new blog posts with image upload and social media link embedding.

#### Components

| Component | Type | Description |
|-----------|------|-------------|
| `MarkdownEditor` | `form` | Rich-text/markdown editor with basic text layout functions |
| `ImageUploader` | `form` | Upload images that are stored in the database |
| `SocialEmbed` | `form` | Insert visually appealing links to social media and video platforms |
| `PublishButton` | `form` | Save draft or publish post |

#### User Actions

- Write post content
- Upload images
- Embed social/video links
- Save/publish post

---

### Post Edit

**Route:** `/posts/:id/edit`

Edit existing post. Authors can edit only their own posts; Admin/root can edit any post.

#### Components

| Component | Type | Description |
|-----------|------|-------------|
| `MarkdownEditor` | `form` | Pre-filled markdown editor for editing post content |
| `ImageUploader` | `form` | Manage uploaded images |
| `DeleteButton` | `form` | Delete post (author for own posts, admin/root for any) |

#### User Actions

- Edit post content
- Manage images
- Save changes
- Delete post

---

### Moderation Queue

**Route:** `/admin/moderation`

Admin/root view of all pending guest comments awaiting approval. Supports individual and bulk approve/reject.

#### Components

| Component | Type | Description |
|-----------|------|-------------|
| `PendingCommentList` | `table` | Table of unapproved guest comments with content, post title, guest name/email |
| `BulkActions` | `form` | Select multiple comments for bulk approve or reject |
| `IndividualActions` | `form` | Approve or reject individual comments |

#### User Actions

- View pending comments
- Approve single comment
- Reject single comment
- Bulk approve
- Bulk reject

---

### Admin Dashboard - User Management

**Route:** `/admin/users`

Root-only page for managing users and their group assignments.

#### Components

| Component | Type | Description |
|-----------|------|-------------|
| `UserList` | `table` | Table of all users with username, email, groups, and actions |
| `GroupAssignment` | `form` | Assign or remove users from groups (Author, Admin, Viewer) |

#### User Actions

- View all users
- Assign user to group
- Remove user from group

---

### Admin Dashboard - Settings

**Route:** `/admin/settings`

Root-only page for configuring system-wide settings like blog title and moderation process.

#### Components

| Component | Type | Description |
|-----------|------|-------------|
| `SettingsForm` | `form` | Form to edit blog title, moderation toggle, and other system settings |

#### User Actions

- Edit blog title
- Toggle moderation process
- Save settings

---

### Swagger / API Documentation

**Route:** `/swagger`

Swagger UI for exploring the Web API endpoints available to Author and Viewer roles.

#### Components

| Component | Type | Description |
|-----------|------|-------------|
| `SwaggerUI` | `navigation` | Interactive API documentation with endpoint testing |

#### User Actions

- Browse API endpoints
- Test API calls

---

## Navigation Structure

- **structure:** Top navigation bar visible on all pages. For unauthenticated users: Home, Login, Register. For authenticated users: Home, New Post (Author only), Moderation Queue (Admin/root only), User Management (root only), Settings (root only), Logout, User Profile menu. The application is a SPA with client-side routing — no full page round-trips.

## Theme & Styling

- **description:** Responsive design optimized for desktop, tablet, and smartphone. Clean blog-style layout with readable typography. Social media and video embeds rendered in visually appealing cards. All functions equally usable on every device class.

---

## How to Modify This Specification

1. Edit the page/component definitions above
2. ZooCode will detect changes to this file and update:
   - UI component files (widgets, pages, views)
   - Routing configuration
   - Navigation components
   - Style/theme files
