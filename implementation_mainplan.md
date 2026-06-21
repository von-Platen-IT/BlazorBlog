# Implementation Main Plan — BlazorBlog

> **Erstellt:** 2026-06-21  
> **Aktualisiert:** 2026-06-21 (Social Login, Root-Detailplanung)  
> **Basis:** [`database-schema.md`](.roo/rules/database-schema.md), [`project-rules.md`](.roo/rules/project-rules.md), [`ui-specification.md`](.roo/rules/ui-specification.md), [`Readme.md`](Readme.md)  
> **Prinzip:** Jeder Schritt ist möglichst unabhängig testbar.

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

### Root-Constraints

| Constraint | Beschreibung |
|------------|-------------|
| Wird geseedet, nicht registriert | Root entsteht nur durch `appsettings.json` → `Blog:RootUsername` / `Blog:RootPassword` |
| Keine Gruppen-Zuordnung | `IsRoot = true` ersetzt alle Gruppen-Checks; Root wird nie einer Gruppe zugewiesen |
| Nicht löschbar | Die API verweigert Löschung des Root-Users |
| Nicht modifizierbar durch andere | Nur Root selbst kann eigenes Passwort ändern; kein anderer User (auch kein Admin) darf Root bearbeiten |
| Kein Social Login für Root | Root authentifiziert sich ausschließlich lokal mit Passwort |

### Root Authorization Flow

```
Request → AuthMiddleware → ClaimsPrincipal
                              ├── IsRoot claim? → YES → ALL access granted
                              └── IsRoot claim? → NO  → Group-based policy check
```

Die `RootPolicy` prüft ausschließlich den `IsRoot`-Claim. Alle Endpoints, die Admin/Root erlauben, verwenden eine kombinierte Policy: `AdminPolicy OR RootPolicy`.

---

## Schritt 1 — Domain Layer (Entitäten & Interfaces)

**Ziel:** Alle Domain-Entitäten, Value Objects und Repository-Interfaces definieren. Keine externen Abhängigkeiten.

| # | Task | Details |
|---|------|---------|
| 1.1 | `AppUser` Entity | `Id`, `UserName`, `Email`, `PasswordHash` (**nullable** — null bei reinen OAuth-Usern), `IsRoot`, `CreatedAt`, `UpdatedAt` |
| 1.2 | `ExternalLogin` Entity | `Id`, `UserId` (FK → AppUser), `Provider` (string: "Google", "GitHub", "Microsoft"), `ProviderKey` (string: OAuth-Subject), `CreatedAt` |
| 1.3 | `Group` Entity | `Id`, `Name`, `Description` |
| 1.4 | `Post` Entity | `Id`, `Title`, `Content`, `AuthorId`, `IsPublished`, `CreatedAt`, `UpdatedAt`, `PublishedAt` |
| 1.5 | `Comment` Entity | `Id`, `Content`, `PostId`, `UserId` (nullable), `ParentCommentId` (nullable), `GuestName`, `GuestEmail`, `IsApproved`, `CreatedAt` |
| 1.6 | `Media` Entity | `Id`, `PostId`, `FileName`, `ContentType`, `Data` (byte[]), `CreatedAt` |
| 1.7 | `SystemSetting` Entity | `Id`, `Key`, `Value`, `UpdatedAt` |
| 1.8 | Navigation Properties | User↔Groups (M:N), User→ExternalLogins (1:N), User→Posts (1:N), User→Comments (1:N), Post→Comments (1:N), Post→Media (1:N), Comment→Replies (Self-ref) |
| 1.9 | Repository Interfaces | `IAppUserRepository`, `IExternalLoginRepository`, `IGroupRepository`, `IPostRepository`, `ICommentRepository`, `IMediaRepository`, `ISystemSettingRepository` |

**Ergebnis:** Kompilierbare Domain-Library mit 7 Entitäten (inkl. ExternalLogin).

---

## Schritt 2 — Infrastructure Layer (EF Core & Datenbank)

**Ziel:** EF Core DbContext, Fluent-API-Konfiguration, Migration, Datenbank-Seeding.

| # | Task | Details |
|---|------|---------|
| 2.1 | `BlogDbContext` | DbContext mit `DbSet<T>` für alle 7 Entitäten |
| 2.2 | Fluent API Konfiguration | `IEntityTypeConfiguration<T>` für jede Entität: Tabellennamen, Spalten, Relationships, Indexes |
| 2.3 | Indexes | Unique: `UserName`, `Group.Name`, `SystemSetting.Key`, `ExternalLogin(Provider, ProviderKey)`; Non-unique: `Post.CreatedAt`, `Post.AuthorId`, `Comment(PostId, IsApproved)`, `Comment.IsApproved`, `Comment.ParentCommentId`, `Media.PostId`, `ExternalLogin.UserId` |
| 2.4 | Connection String | Aus `appsettings.json` → `ConnectionStrings:DefaultConnection` laden |
| 2.5 | Initial Migration | `dotnet ef migrations add InitialCreate` |
| 2.6 | Auto-Migration on Startup | `DbContext.Database.Migrate()` in `Program.cs` |
| 2.7 | Seed Data | Root-User seeden (IsRoot=true, keine Gruppen); Groups seeden (Author, Admin, Viewer, Public); Default SystemSettings seeden |

**Ergebnis:** Datenbank wird beim ersten Start automatisch erstellt und mit Root + Gruppen + Settings befüllt.

---

## Schritt 3 — Authentication & Authorization

**Ziel:** Lokale Registrierung/Login, OAuth 2.0 Social Login, JWT+Cookie-Auth, Rollen-Policies inkl. Root.

### 3A — Lokale Authentifizierung

| # | Task | Details |
|---|------|---------|
| 3A.1 | Password Hashing | BCrypt via `BCrypt.Net-Next` NuGet Package |
| 3A.2 | `AuthService` — Registrierung | Neuen User anlegen, Passwort hashen, Default-Gruppe "Viewer" zuweisen |
| 3A.3 | `AuthService` — Login | Credentials prüfen (auch Root), JWT + Cookie ausstellen |
| 3A.4 | JWT Konfiguration | Token mit Claims: `sub` (UserId), `username`, `email`, `groups` (Array), `is_root` (bool) |
| 3A.5 | Cookie Konfiguration | Cookie-Auth für Blazor SPA parallel zu JWT |
| 3A.6 | `CurrentUserService` | Scoped Service: `UserId`, `UserName`, `IsRoot`, `Groups` aus HttpContext |

### 3B — OAuth 2.0 / OpenID Connect Social Login

| # | Task | Details |
|---|------|---------|
| 3B.1 | OAuth Infrastructure | `Microsoft.AspNetCore.Authentication.Google`, `.GitHub`, `.Microsoft` NuGet Packages |
| 3B.2 | OAuth Konfiguration | `appsettings.json` → `Authentication:Google:ClientId/ClientSecret` (analog für GitHub, Microsoft); nur konfigurierte Provider sind aktiv |
| 3B.3 | OAuth Challenge Endpoint | `GET /api/auth/login/{provider}` — leitet zum OAuth-Provider weiter (Google, GitHub, Microsoft) |
| 3B.4 | OAuth Callback Handler | `GET /api/auth/callback/{provider}` — empfängt OAuth-Code, tauscht gegen Token, extrahiert Claims (sub, email, name) |
| 3B.5 | User-Resolution Logik | Finde existierenden User per `ExternalLogin(Provider, ProviderKey)`; wenn nicht gefunden: prüfe ob User mit gleicher Email existiert → verknüpfe; sonst: lege neuen User an (UserName = email-prefix, Email = OAuth-Email, PasswordHash = null) |
| 3B.6 | Account-Verknüpfung | Authentifizierte User können nachträglich OAuth-Provider verknüpfen: `POST /api/auth/link/{provider}` |
| 3B.7 | Account-Entknüpfung | `DELETE /api/auth/unlink/{provider}` — nur wenn User noch ein lokales Passwort ODER einen anderen OAuth-Provider hat |
| 3B.8 | Root-Ausschluss | Root-User kann keinen Social Login verwenden oder verknüpfen |

### 3C — Authorization Policies

| # | Task | Details |
|---|------|---------|
| 3C.1 | `RootPolicy` | Prüft `IsRoot`-Claim → `true` = alle Rechte |
| 3C.2 | `AdminPolicy` | Prüft Group-Claim auf "Admin" → `true` = Admin-Rechte |
| 3C.3 | `AuthorPolicy` | Prüft Group-Claim auf "Author" → `true` = Author-Rechte |
| 3C.4 | `ViewerPolicy` | Prüft Group-Claim auf "Viewer" → `true` = Viewer-Rechte |
| 3C.5 | `AdminOrRootPolicy` | Kombiniert AdminPolicy + RootPolicy für alle Admin-Endpoints |
| 3C.6 | `AuthorOrAdminOrRootPolicy` | Für Post-Edit/Delete: Author (own) OR Admin OR Root |

**Ergebnis:** Benutzer können sich lokal registrieren oder per Google/GitHub/Microsoft einloggen. Root hat uneingeschränkten Zugriff.

---

## Schritt 4 — Blog Posts (CRUD + API)

**Ziel:** Autoren können Posts erstellen/bearbeiten/löschen; alle können publizierte Posts lesen. Root/Admin haben Vollzugriff.

| # | Task | Details |
|---|------|---------|
| 4.1 | `PostRepository` | EF Core Implementierung: Create, Update, Delete, GetById, GetPublished (paginiert), GetByAuthor |
| 4.2 | Post Commands/Queries (CQRS) | `CreatePost`, `UpdatePost`, `DeletePost`, `PublishPost`, `GetPostById`, `GetPublishedPosts` — mit MediatR |
| 4.3 | Post Validators (FluentValidation) | Titel required, Content required, AuthorId required |
| 4.4 | Post API Endpoints | `GET /api/posts` (public, paginiert), `GET /api/posts/{id}` (public), `POST /api/posts` (Author/Admin/Root), `PUT /api/posts/{id}` (Author own / Admin/Root all), `DELETE /api/posts/{id}` (Author own / Admin/Root all) |
| 4.5 | Root/Admin Override | `DeletePost`-Command prüft: `currentUser.IsRoot || currentUser.Groups.Contains("Admin") || post.AuthorId == currentUser.UserId` |

**Ergebnis:** Vollständige Post-CRUD-API mit Rollen-Autorisierung inkl. Root-Override.

---

## Schritt 5 — Media Upload (Bilder in DB)

**Ziel:** Bilder-Upload für Blog-Posts, Speicherung als byte[] in der Media-Tabelle.

| # | Task | Details |
|---|------|---------|
| 5.1 | `MediaRepository` | Create, GetByPostId, Delete |
| 5.2 | Media Commands | `UploadMedia`, `DeleteMedia` |
| 5.3 | Validation | Erlaubte Content-Types (image/jpeg, image/png, image/gif, image/webp), max. Dateigröße 5 MB |
| 5.4 | Media API Endpoints | `POST /api/posts/{postId}/media` (Author/Admin/Root), `GET /api/posts/{postId}/media` (public), `DELETE /api/media/{id}` (Author own / Admin/Root all) |

**Ergebnis:** Bilder können zu Posts hochgeladen und abgerufen werden.

---

## Schritt 6 — Kommentarsystem (Nested, Moderation)

**Ziel:** Verschachtelte Kommentare, Auto-Approve für authentifizierte User, Moderation für Gäste. Root/Admin moderieren.

| # | Task | Details |
|---|------|---------|
| 6.1 | `CommentRepository` | Create, GetByPostId (approved, tree), GetPending (moderation), Approve, Reject, Delete |
| 6.2 | Comment Commands/Queries | `AddComment`, `ApproveComment`, `RejectComment`, `GetCommentsForPost`, `GetPendingComments` |
| 6.3 | Comment Validators | Content required, PostId required; GuestName+GuestEmail required wenn UserId null |
| 6.4 | Auto-Approve Logik | `IsApproved = UserId != null` (authenticated → true, guest → false) |
| 6.5 | Comment API Endpoints | `GET /api/posts/{postId}/comments` (public, nur approved), `POST /api/posts/{postId}/comments` (public/authenticated), `POST /api/comments/{id}/reply` (public/authenticated) |
| 6.6 | Moderation API Endpoints | `GET /api/admin/comments/pending` (Admin/Root), `POST /api/admin/comments/{id}/approve`, `POST /api/admin/comments/{id}/reject`, `POST /api/admin/comments/bulk-approve`, `POST /api/admin/comments/bulk-reject` |

**Ergebnis:** Vollständiges Kommentarsystem mit Nested Replies und Moderation-Workflow.

---

## Schritt 7 — User & Group Management (Root only)

**Ziel:** Root kann alle Benutzer verwalten und Gruppen zuweisen. Root selbst ist geschützt.

| # | Task | Details |
|---|------|---------|
| 7.1 | `UserRepository` | GetAll, GetById, Update (Groups), Delete — mit Root-Schutz |
| 7.2 | User Management Commands | `GetAllUsers`, `GetUserById`, `AssignUserToGroup`, `RemoveUserFromGroup`, `DeleteUser` |
| 7.3 | Root-Schutz in Commands | `AssignUserToGroup` verweigert Zuweisung wenn Target-User `IsRoot == true`; `DeleteUser` verweigert Löschung des Root-Users |
| 7.4 | User Management API | `GET /api/admin/users` (Root), `GET /api/admin/users/{id}` (Root), `POST /api/admin/users/{id}/groups` (Root), `DELETE /api/admin/users/{id}/groups/{groupId}` (Root), `DELETE /api/admin/users/{id}` (Root, nicht für Root selbst) |

**Ergebnis:** Root verwaltet alle Benutzer; Root selbst ist unantastbar.

---

## Schritt 8 — System Settings (Root only)

**Ziel:** Root kann Blog-Titel, Moderations-Toggle und OAuth-Provider-Konfiguration verwalten.

| # | Task | Details |
|---|------|---------|
| 8.1 | `SystemSettingRepository` | GetAll, GetByKey, Set |
| 8.2 | Settings Commands | `GetAllSettings`, `UpdateSetting` |
| 8.3 | Settings API | `GET /api/admin/settings` (Root), `PUT /api/admin/settings/{key}` (Root) |
| 8.4 | Seed Default Settings | `BlogTitle` = "My Blog", `ModerationEnabled` = "true" |
| 8.5 | OAuth Settings | `Google:ClientId`, `Google:ClientSecret`, `GitHub:ClientId`, `GitHub:ClientSecret`, `Microsoft:ClientId`, `Microsoft:ClientSecret` — über Settings-API verwaltbar |

**Ergebnis:** Root kann alle Systemeinstellungen per API ändern.

---

## Schritt 9 — Swagger / OpenAPI

**Ziel:** API-Dokumentation mit Native OpenAPI und Swagger UI.

| # | Task | Details |
|---|------|---------|
| 9.1 | OpenAPI Konfiguration | `builder.Services.AddOpenApi()` in Presentation |
| 9.2 | Swagger UI | `app.MapOpenApi()` + Swagger UI unter `/swagger` |
| 9.3 | JWT + OAuth in Swagger | Bearer-Token-Eingabe + OAuth2-Flows in Swagger UI dokumentieren |

**Ergebnis:** Interaktive API-Dokumentation unter `/swagger`.

---

## Schritt 10 — Blazor SPA Frontend

**Ziel:** Responsive Single-Page-Application mit Blazor und BlazorStrap.

| # | Task | Details |
|---|------|---------|
| 10.1 | Blazor Konfiguration | Blazor Server in Presentation einrichten, Routing, Layout |
| 10.2 | TopNav Komponente | Navigation: Home, Login/Register (unauth), New Post (Author/Admin/Root), Moderation (Admin/Root), Users (Root), Settings (Root), Logout, User-Profil |
| 10.3 | Home / Post List (`/`) | `PostList` + `Pagination` — chronologische Liste publizierter Posts |
| 10.4 | Post Detail (`/posts/{id}`) | `PostContent` (Markdown-Rendering), `CommentSection` (Nested Tree), `CommentForm`, `ReplyForm` |
| 10.5 | Login (`/login`) | `LoginForm` (Username/Password) + **Social Login Buttons** (Google, GitHub, Microsoft — nur wenn konfiguriert) |
| 10.6 | Register (`/register`) | `RegisterForm` (Username, Email, Password) + **Social Login Buttons** |
| 10.7 | Post Editor (`/posts/new`) | `MarkdownEditor`, `ImageUploader`, `SocialEmbed`, `PublishButton` |
| 10.8 | Post Edit (`/posts/{id}/edit`) | Gleiche Komponenten wie Editor, vorausgefüllt, `DeleteButton` |
| 10.9 | Moderation Queue (`/admin/moderation`) | `PendingCommentList` (Tabelle), `BulkActions`, `IndividualActions` |
| 10.10 | User Management (`/admin/users`) | `UserList` (Tabelle), `GroupAssignment` — Root-User visuell markiert, keine Aktionen auf Root möglich |
| 10.11 | Settings (`/admin/settings`) | `SettingsForm` — BlogTitle, ModerationEnabled, OAuth-Provider-Config |
| 10.12 | User Profile (`/profile`) | Eigenes Profil anzeigen, Passwort ändern (nur lokale User), OAuth-Provider verknüpfen/entknüpfen |
| 10.13 | Responsive Design | BlazorStrap Grid + CSS für Desktop, Tablet, Smartphone |
| 10.14 | Markdown Rendering | Markdown → HTML mit Embed-Karten für Social Media/Video-Links |
| 10.15 | Client-Side Routing | SPA ohne Full-Page-Roundtrips, alle Navigation per Blazor Router |

**Ergebnis:** Vollständige, responsive Blog-SPA mit Social Login und Root-Admin-Funktionen.

---

## Abhängigkeiten der Schritte

```
Schritt 1 (Domain: 7 Entities inkl. ExternalLogin)
  └─→ Schritt 2 (Infrastructure/EF Core + Migration + Seed)
       └─→ Schritt 3 (Auth: Lokal + OAuth + Policies inkl. RootPolicy)
            ├─→ Schritt 4 (Posts API)
            │    └─→ Schritt 5 (Media Upload)
            ├─→ Schritt 6 (Comments API + Moderation)
            ├─→ Schritt 7 (User Management — Root only)
            ├─→ Schritt 8 (System Settings — Root only)
            └─→ Schritt 9 (Swagger)
                 └─→ Schritt 10 (Blazor Frontend)
```

- **Schritt 1** ist Fundament für alles.
- **Schritt 2** baut auf 1 auf.
- **Schritt 3** wird von 4–9 benötigt (Auth + Root-Policies).
- **Schritte 4–8** sind untereinander weitgehend unabhängig.
- **Schritt 9** ist unabhängig von 4–8, braucht nur 3.
- **Schritt 10** integriert alle API-Schritte (4–9) ins Frontend.

---

## Nicht im Scope

- ❌ Kein E-Mail-Versand (Verification, Password Reset, Notification)
- ❌ Kein RSS/Atom Feed
- ❌ Kein Tag/Category-System für Posts
- ❌ Kein Suchsystem
- ❌ Kein Draft/Preview-System (nur IsPublished true/false)
- ❌ Kein Analytics/Tracking
- ❌ Kein CDN/Cloud-Storage für Bilder
- ❌ Kein Caching (kommt ggf. später als Optimierung)
- ❌ Keine weiteren OAuth-Provider außer Google, GitHub, Microsoft