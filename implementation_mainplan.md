# Implementation Main Plan — BlazorBlog

> **Erstellt:** 2026-06-21  
> **Aktualisiert:** 2026-06-25 (Rating, Bookmarks, My Posts, Comment Redesign)  
> **Basis:** [`database-schema.md`](.roo/rules/database-schema.md), [`project-rules.md`](.roo/rules/project-rules.md), [`ui-specification.md`](.roo/rules/ui-specification.md), [`Readme.md`](Readme.md)  
> **Prinzip:** Jeder Schritt ist möglichst unabhängig testbar. Die Applikation soll sich wie eine Desktop-Anwendung anfühlen — keine Full-Page-Roundtrips, alle Interaktionen über Blazor Interactive Server mit SignalR.

---

## Architektur-Philosophie: SPA ohne Round-Trips

Die gesamte Anwendung ist als **Single Page Application** mit **Blazor Interactive Server** (`@rendermode InteractiveServer`) konzipiert. Sämtliche Benutzerinteraktionen — Kommentare posten, Beiträge bewerten, Bookmarks setzen, zwischen Seiten navigieren — erfolgen **ohne vollständige Page-Reloads**. Die UI wird über SignalR live aktualisiert.

**Modernste Razor-Techniken im Einsatz:**
- `@rendermode InteractiveServer` für alle interaktiven Seiten
- `async`/`await` in `@onclick`-Handlern für sofortige UI-Updates
- **Optimistic UI Updates:** API-Antworten werden direkt in lokale Listen eingefügt, kein `GET`-Reload
- Gekapselte Blazor-Komponenten mit direktem `ApiClient`-Injection (kein Event-Bubbling)

---

## Root Superuser — Übergreifende Spezifikation

Der Root-User ist ein systemweit einmaliger Superuser, der beim ersten Start geseedet wird. Er ist **keiner Gruppe** zugeordnet und erhält alle Berechtigungen über das `IsRoot`-Flag.

### Root-Berechtigungen (vollständig)

| Bereich | Berechtigung | Details |
|---------|-------------|---------|
| **Posts** | Alle CRUD-Operationen | Darf jeden Post erstellen, bearbeiten, löschen, veröffentlichen — unabhängig vom Autor |
| **Kommentare** | Alle Moderationen | Darf alle Kommentare genehmigen, ablehnen, löschen — Bulk und Einzeln |
| **Benutzer** | Vollständige Verwaltung | Alle Benutzer auflisten, anzeigen; Gruppen zuweisen/entfernen; Root selbst kann nicht gelöscht oder modifiziert werden |
| **Systemeinstellungen** | Vollständige Kontrolle | Alle Settings lesen und schreiben (BlogTitle, ModerationEnabled, OAuth-Config) |
| **Media** | Alle Operationen | Darf jede Media-Datei löschen |
| **Ratings & Bookmarks** | Alle Operationen | Darf alle Ratings und Bookmarks einsehen und löschen |

### Root Authorization Flow

```
Request → AuthMiddleware → ClaimsPrincipal
                              ├── IsRoot claim? → YES → ALL access granted
                              └── IsRoot claim? → NO  → Group-based policy check
```

---

## Schritt 1 — Domain Layer (Entitäten & Interfaces)

**Ziel:** Alle Domain-Entitäten, Value Objects und Repository-Interfaces definieren. Keine externen Abhängigkeiten.

| # | Task | Details |
|---|------|---------|
| 1.1 | `AppUser` Entity | `Id`, `UserName`, `Email`, `PasswordHash`, `IsRoot`, `CreatedAt`, `UpdatedAt` |
| 1.2 | `ExternalLogin` Entity | `Id`, `UserId`, `Provider`, `ProviderKey`, `CreatedAt` |
| 1.3 | `Group` Entity | `Id`, `Name`, `Description` |
| 1.4 | `Post` Entity | `Id`, `Title`, `Content`, `AuthorId`, `IsPublished`, `CreatedAt`, `UpdatedAt`, `PublishedAt` |
| 1.5 | `Comment` Entity | `Id`, `Content`, `PostId`, `UserId` (nullable), `ParentCommentId` (nullable), `GuestName`, `GuestEmail`, `IsApproved`, `CreatedAt` |
| 1.6 | `PostRating` Entity | `Id`, `PostId`, `UserId`, `IsLike`, `CreatedAt` — Unique(PostId, UserId) |
| 1.7 | `Bookmark` Entity | `Id`, `PostId`, `UserId`, `CreatedAt` — Unique(PostId, UserId) |
| 1.8 | `Media` Entity | `Id`, `PostId`, `FileName`, `ContentType`, `Data` (byte[]), `CreatedAt` |
| 1.9 | `SystemSetting` Entity | `Id`, `Key`, `Value`, `UpdatedAt` |
| 1.10 | Navigation Properties | User↔Groups (M:N), User→ExternalLogins (1:N), User→Posts (1:N), User→Comments (1:N), Post→Comments (1:N), Post→Media (1:N), Post→Ratings (1:N), Post→Bookmarks (1:N), Comment→Replies (Self-ref) |
| 1.11 | Repository Interfaces | 9 Interfaces: `IAppUserRepository`, `IExternalLoginRepository`, `IGroupRepository`, `IPostRepository`, `ICommentRepository`, `IPostRatingRepository`, `IBookmarkRepository`, `IMediaRepository`, `ISystemSettingRepository` |

**Ergebnis:** Kompilierbare Domain-Library mit 9 Entitäten.

---

## Schritt 2 — Infrastructure Layer (EF Core & Datenbank)

**Ziel:** EF Core DbContext, Fluent-API-Konfiguration, Migration, Datenbank-Seeding.

| # | Task | Details |
|---|------|---------|
| 2.1 | `BlogDbContext` | DbContext mit `DbSet<T>` für alle 9 Entitäten |
| 2.2 | Fluent API Konfiguration | `IEntityTypeConfiguration<T>` für jede Entität |
| 2.3 | Indexes | Unique: `UserName`, `Group.Name`, `SystemSetting.Key`, `ExternalLogin(Provider, ProviderKey)`, `PostRating(PostId, UserId)`, `Bookmark(PostId, UserId)`; Non-unique: `Post.CreatedAt`, `Post.AuthorId`, `Comment(PostId, IsApproved)`, `Comment.IsApproved`, `Comment.ParentCommentId`, `Media.PostId`, `PostRating.PostId`, `Bookmark.UserId` |
| 2.4 | Initial Migration | `dotnet ef migrations add InitialCreate` + `AddRatingsAndBookmarks` |
| 2.5 | Auto-Migration on Startup | `DbContext.Database.Migrate()` in `Program.cs` |
| 2.6 | Seed Data | Root-User, Groups, Default SystemSettings |

---

## Schritt 3 — Authentication & Authorization

**Ziel:** Lokale Registrierung/Login, OAuth 2.0 Social Login, JWT+Cookie-Auth, Rollen-Policies inkl. Root.

| # | Task | Details |
|---|------|---------|
| 3.1 | Password Hashing | BCrypt via `BCrypt.Net-Next` |
| 3.2 | `AuthService` | Registrierung, Login, JWT + Cookie |
| 3.3 | `CurrentUserService` | Scoped Service: `UserId`, `UserName`, `IsRoot`, `Groups` |
| 3.4 | OAuth Providers | Google, GitHub, Microsoft (nur wenn konfiguriert) |
| 3.5 | Authorization Policies | `RootPolicy`, `AdminPolicy`, `AuthorPolicy`, `AdminOrRootPolicy`, `AuthorOrAdminOrRootPolicy` |

---

## Schritt 4 — Blog Posts (CRUD + API + My Posts)

**Ziel:** Autoren können Posts erstellen/bearbeiten/löschen; alle können publizierte Posts lesen. Root/Admin haben Vollzugriff.

| # | Task | Details |
|---|------|---------|
| 4.1 | `PostRepository` | Create, Update, Delete, GetById, GetPublished (paginiert), GetByAuthor, GetByAuthorIdPaginated |
| 4.2 | Post API Endpoints | `GET /api/posts` (public, mit Rating-Counts), `GET /api/posts/{id}`, `POST /api/posts`, `PUT /api/posts/{id}`, `DELETE /api/posts/{id}` |
| 4.3 | My Posts Endpoint | `GET /api/posts/my` — paginierte eigene Posts (Drafts + Published) mit Comment-Counts |
| 4.4 | Draft Access | `GET /api/posts/{id}` erlaubt Autoren-Zugriff auf eigene Drafts |

---

## Schritt 5 — Media Upload (Bilder in DB)

**Ziel:** Bilder-Upload für Blog-Posts, Speicherung als byte[] in der Media-Tabelle.

| # | Task | Details |
|---|------|---------|
| 5.1 | `MediaRepository` | Create, GetByPostId, Delete |
| 5.2 | Validation | Erlaubte Content-Types, max. 5 MB |
| 5.3 | Media API | `POST /api/media/post/{postId}`, `GET /api/media/post/{postId}`, `GET /api/media/{id}/data`, `DELETE /api/media/{id}` |

---

## Schritt 6 — Kommentarsystem (Modul, Tree-Lines, Optimistic Updates)

**Ziel:** Verschachtelte Kommentare mit visuellen Tree-Lines, Sofort-Feedback, Moderation für Gäste. Als gekapseltes Blazor-Modul.

| # | Task | Details |
|---|------|---------|
| 6.1 | `CommentRepository` | Create, GetByPostId (approved, tree), GetPending, Approve, Reject, Delete, GetCommentCountsByPostIds |
| 6.2 | `CommentSection.razor` | **Modul-Wurzel:** Lädt Kommentar-Baum, Top-Level-Formular, rendert `CommentNode`-Liste. Optimistic Insert: neuer Kommentar sofort in Liste. |
| 6.3 | `CommentNode.razor` | **Rekursiver Kommentar:** Inline-Reply-Formular, direkter `ApiClient`-Aufruf (kein Event-Bubbling), Tree-Lines via CSS |
| 6.4 | Tree-Line CSS | Vertikale + horizontale graue Linien pro Verschachtelungsebene, responsive |
| 6.5 | Auto-Approve Logik | `IsApproved = UserId != null` |
| 6.6 | Gast-Kommentare | Sofort sichtbar mit ⏳ "Pending Approval" Badge |
| 6.7 | Comment API | `GET /api/comments/post/{postId}`, `POST /api/comments/post/{postId}`, `POST /api/comments/{id}/reply` |
| 6.8 | Moderation API | `GET /api/admin/comments/pending`, `POST /api/admin/comments/{id}/approve`, `POST /api/admin/comments/{id}/reject`, Bulk-Operationen |

---

## Schritt 7 — Rating & Bookmark System

**Ziel:** Authentifizierte Benutzer können Beiträge liken/disliken und bookmarken. Alle Besucher sehen die Zähler.

| # | Task | Details |
|---|------|---------|
| 7.1 | `PostRatingRepository` | GetByPostAndUser, GetCounts, GetCountsByPostIds, Add, Update, Delete |
| 7.2 | `BookmarkRepository` | GetByPostAndUser, GetBookmarkedPosts (paginiert), Add, Delete |
| 7.3 | Rating API | `POST /api/posts/{id}/like`, `POST /api/posts/{id}/dislike`, `GET /api/posts/{id}/rating` |
| 7.4 | Bookmark API | `POST /api/posts/{id}/bookmark`, `GET /api/posts/{id}/bookmark`, `GET /api/posts/bookmarks/list` |
| 7.5 | UI: PostDetail | 👍👎🔖 Rating/Bookmark-Bar zwischen Content und Comments |
| 7.6 | UI: Home | Like/Dislike-Counts auf Post-Karten |
| 7.7 | UI: MyPosts | "🔖 Bookmarks"-Tab mit eigener Tabelle/Karten-Ansicht |

---

## Schritt 8 — User & Group Management (Root only)

**Ziel:** Root kann alle Benutzer verwalten und Gruppen zuweisen. Root selbst ist geschützt.

| # | Task | Details |
|---|------|---------|
| 8.1 | `UserRepository` | GetAll, GetById, Update, Delete — mit Root-Schutz |
| 8.2 | User Management API | `GET /api/admin/users`, `GET /api/admin/users/{id}`, `POST /api/admin/users/{id}/groups`, `DELETE /api/admin/users/{id}/groups/{groupId}`, `DELETE /api/admin/users/{id}` |

---

## Schritt 9 — System Settings (Root only)

**Ziel:** Root kann Blog-Titel, Moderations-Toggle und OAuth-Provider-Konfiguration verwalten.

| # | Task | Details |
|---|------|---------|
| 9.1 | Settings API | `GET /api/admin/settings`, `PUT /api/admin/settings/{key}` |
| 9.2 | Seed Default Settings | `BlogTitle` = "My Blog", `ModerationEnabled` = "true" |

---

## Schritt 10 — Swagger / OpenAPI

**Ziel:** API-Dokumentation mit Native OpenAPI und Swagger UI unter `/swagger`.

---

## Schritt 11 — Blazor SPA Frontend

**Ziel:** Responsive Single-Page-Application mit Blazor Interactive Server. Fühlt sich wie eine Desktop-App an.

| # | Task | Details |
|---|------|---------|
| 11.1 | Blazor Konfiguration | `@rendermode InteractiveServer` für alle Seiten, SignalR |
| 11.2 | TopNav Komponente | Navigation: Home, My Posts, New Post, Moderation, Users, Settings, Profile |
| 11.3 | Home (`/`) | `PostList` + `Pagination` + Like/Dislike-Counts |
| 11.4 | Post Detail (`/posts/{id}`) | `PostContent`, `RatingBar`, `BookmarkButton`, `<CommentSection>` Modul |
| 11.5 | My Posts (`/my/posts`) | `PostTable` + `BookmarkTable`, Filter (All/Published/Drafts/Bookmarks), Quick-Publish, Delete-Modal |
| 11.6 | Login/Register | Formulare + Social Login Buttons |
| 11.7 | Post Editor (`/posts/new`, `/posts/{id}/edit`) | Markdown-Editor, Draft/Publish |
| 11.8 | Moderation (`/admin/moderation`) | `PendingCommentList`, Bulk- + Einzel-Actions |
| 11.9 | User Management (`/admin/users`) | `UserList`, `GroupAssignment` |
| 11.10 | Settings (`/admin/settings`) | `SettingsForm` |
| 11.11 | Responsive Design | Bootstrap 5 Grid + CSS für Desktop, Tablet, Smartphone |
| 11.12 | Anti-Roundtrip | Alle Interaktionen per `async`/`await` in `@onclick`, optimistic UI updates, kein `NavigationManager`-Reload |

---

## Abhängigkeiten der Schritte

```
Schritt 1 (Domain: 9 Entities)
  └─→ Schritt 2 (Infrastructure/EF Core + Migration + Seed)
       └─→ Schritt 3 (Auth: Lokal + OAuth + Policies)
            ├─→ Schritt 4 (Posts API + My Posts)
            │    └─→ Schritt 5 (Media Upload)
            ├─→ Schritt 6 (Comments API + Modul)
            ├─→ Schritt 7 (Rating + Bookmarks)
            ├─→ Schritt 8 (User Management)
            ├─→ Schritt 9 (System Settings)
            └─→ Schritt 10 (Swagger)
                 └─→ Schritt 11 (Blazor Frontend)
```

---

## Nicht im Scope

- ❌ Kein E-Mail-Versand (Verification, Password Reset, Notification)
- ❌ Kein RSS/Atom Feed
- ❌ Kein Tag/Category-System für Posts
- ❌ Kein Suchsystem
- ❌ Kein Analytics/Tracking
- ❌ Kein CDN/Cloud-Storage für Bilder
- ❌ Kein Caching (kommt ggf. später als Optimierung)
- ❌ Keine weiteren OAuth-Provider außer Google, GitHub, Microsoft