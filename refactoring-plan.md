# BlazorBlog — Comprehensive Refactoring Plan

> **Analyzed by:** Solution Architect  
> **Date:** 2026-06-25  
> **Tech:** .NET 10, ASP.NET Core, Blazor InteractiveServer, PostgreSQL, EF Core

---

## Executive Summary

The project follows Clean Architecture on paper (Domain → Application → Infrastructure → Presentation), but the **Presentation layer violates separation of concerns** by hosting all API endpoints inline in [`Program.cs`](src/AspBaseProj.Presentation/Program.cs:1). The Blazor Server app calls its own API via HTTP (self-API anti-pattern), the DTOs are strewn across wrong layers, the markdown rendering is primitive, authentication mixes form-post redirects with AJAX, and many specified UI components are missing or incomplete.

This plan identifies **13 key refactoring areas**, ordered by priority and dependency.

---

## Refactoring 1: Extract API Endpoints into Proper Controllers

**Severity:** 🔴 Critical  
**Effort:** Large  
**Files affected:** [`Program.cs`](src/AspBaseProj.Presentation/Program.cs:148) (820+ lines), new `Controllers/` directory

### Problem

All 30+ API endpoints are defined as inline lambda handlers in [`Program.cs`](src/AspBaseProj.Presentation/Program.cs:148), making the file unmanageable at 820+ lines. This violates separation of concerns — routing, request parsing, authorization, and response formatting are mixed in one place.

### Solution

Extract endpoints into **feature-grouped controller classes** using `[ApiController]` attributes:

```
src/AspBaseProj.Presentation/Controllers/
├── AuthController.cs          # /api/auth/*
├── PostsController.cs         # /api/posts/*
├── CommentsController.cs      # /api/comments/*
├── ModerationController.cs    # /api/admin/comments/*
├── UsersController.cs         # /api/admin/users/*
├── SettingsController.cs      # /api/admin/settings/*
└── MediaController.cs         # /api/media/*
```

Each controller injects repository interfaces and `CurrentUserService` directly. The `Program.cs` is reduced to configuration, service registration, and middleware.

### Migration Strategy

1. Move each endpoint group to its own controller
2. Replace anonymous DTOs with typed response records from Application layer
3. Use `[Authorize]` attributes on controllers/methods instead of inline `.RequireAuthorization()`
4. Keep minimal API only for OAuth callback routes (they need special `HttpContext` access)

---

## Refactoring 2: Eliminate Self-API Anti-Pattern

**Severity:** 🔴 Critical  
**Effort:** Medium  
**Files affected:** [`ApiClient.cs`](src/AspBaseProj.Presentation/Components/Shared/ApiClient.cs:1), all `.razor` pages, [`CookieForwardingHandler.cs`](src/AspBaseProj.Presentation/Components/Shared/CookieForwardingHandler.cs:1)

### Problem

Blazor InteractiveServer runs on the server and has **direct access to all services and repositories**. Yet the app creates an `HttpClient` that calls `http://localhost:5113/api/posts/...` to hit its own endpoints. This causes:

- Unnecessary HTTP overhead (serialization, transport, deserialization)
- Cookie forwarding complexity (`CookieForwardingHandler`)
- Dual code paths (both API and direct service injection)
- Confusion between "Blazor Server" and "SPA" architecture

### Solution

Replace `HttpClient` + `ApiClient` calls with **direct service/repository injection** in Blazor components:

```razor
@* Before *@
@inject ApiClient Api
var post = await Api.GetPostAsync(Id);

@* After *@
@inject IPostRepository PostRepo
@inject IPostRatingRepository RatingRepo
@inject CurrentUserService CurrentUser
var post = await PostRepo.GetByIdAsync(Id);
```

**Keep `ApiClient` only for truly remote calls** (e.g., if the SPA is decoupled in the future).

### Migration Strategy

1. Add `@rendermode InteractiveServer` (already present) ensures server-side execution
2. Replace `ApiClient` calls with direct repository/service injection
3. Remove `CookieForwardingHandler`, `HttpClient` registration from DI
4. Simplify `Program.cs` — remove the custom `HttpClient` setup (lines 27-35)
5. Keep `ApiClient.cs` as a thin wrapper for potential future SPA separation, but deprecate usage

---

## Refactoring 3: Move DTOs to Application Layer

**Severity:** 🟠 High  
**Effort:** Medium  
**Files affected:** [`ApiClient.cs`](src/AspBaseProj.Presentation/Components/Shared/ApiClient.cs:250) (DTO records), [`Program.cs`](src/AspBaseProj.Presentation/Program.cs:798) (anonymous types)

### Problem

API response DTOs are defined as `record` types inside [`ApiClient.cs`](src/AspBaseProj.Presentation/Components/Shared/ApiClient.cs:250) (Presentation layer), and inline anonymous types in [`Program.cs`](src/AspBaseProj.Presentation/Program.cs:798). This makes them:

- Unavailable to other layers
- Duplicated if shared between API and Blazor
- Not part of any explicit API contract

### Solution

Create a dedicated `Contracts/` folder in the Application layer:

```
src/AspBaseProj.Application/Contracts/
├── Auth/
│   ├── LoginRequest.cs
│   ├── RegisterRequest.cs
│   └── AuthResponse.cs
├── Posts/
│   ├── PostDto.cs
│   ├── PostListResponse.cs
│   └── PostCreateRequest.cs
├── Comments/
│   ├── CommentDto.cs
│   └── CreateCommentRequest.cs
├── Ratings/
│   └── RatingResponse.cs
├── Bookmarks/
│   └── BookmarkResponse.cs
└── Common/
    └── PagedResponse.cs
```

All controllers and Blazor components reference these shared contracts.

---

## Refactoring 4: Add Proper Markdown Rendering

**Severity:** 🟠 High  
**Effort:** Small  
**Files affected:** [`PostDetail.razor`](src/AspBaseProj.Presentation/Components/Pages/PostDetail.razor:232)

### Problem

The `RenderMarkdown()` method at line 232 does **basic string replacements** only — it cannot handle:
- Code blocks (``` ```)
- Headers (`#`, `##`)
- Lists (ordered/unordered)
- Links and images
- Blockquotes
- Tables

### Solution

Add the [Markdig](https://github.com/xoofx/markdig) NuGet package and use its proper markdown-to-HTML pipeline:

```csharp
private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions() // tables, footnotes, etc.
    .Build();

private static MarkupString RenderMarkdown(string markdown)
{
    var html = Markdown.ToHtml(markdown, Pipeline);
    return new MarkupString(html);
}
```

Add CSS styling for rendered markdown elements in [`app.css`](src/AspBaseProj.Presentation/wwwroot/css/app.css).

---

## Refactoring 5: Extract Reusable Blazor Components from Pages

**Severity:** 🟠 High  
**Effort:** Medium  
**Files affected:** All pages, new component files

### Problem

The UI spec defines specific components (RatingBar, BookmarkButton, Pagination, PostContent, etc.) but they are **inlined directly in pages**, causing:
- Duplicate code (pagination appears in Home + MyPosts)
- No reusability
- Difficult maintenance
- No clear component API (parameters, events)

### Solution

Extract these components:

| Component | Source | Parameters |
|-----------|--------|------------|
| [`PostList.razor`](src/AspBaseProj.Presentation/Components/Shared/PostList.razor) | Home.razor | `Posts`, `OnViewPost` |
| [`Pagination.razor`](src/AspBaseProj.Presentation/Components/Shared/Pagination.razor) | Home.razor + MyPosts.razor | `CurrentPage`, `TotalPages`, `OnPageChanged` |
| [`RatingBar.razor`](src/AspBaseProj.Presentation/Components/Shared/RatingBar.razor) | PostDetail.razor | `PostId`, `LikeCount`, `DislikeCount`, `UserRating`, `IsAuthenticated` |
| [`BookmarkButton.razor`](src/AspBaseProj.Presentation/Components/Shared/BookmarkButton.razor) | PostDetail.razor | `PostId`, `IsBookmarked`, `IsAuthenticated` |
| [`PostContent.razor`](src/AspBaseProj.Presentation/Components/Shared/PostContent.razor) | PostDetail.razor | `Post` |
| [`MarkdownEditor.razor`](src/AspBaseProj.Presentation/Components/Shared/MarkdownEditor.razor) | PostEditor.razor | `Content`, `ContentChanged` |
| [`ImageUploader.razor`](src/AspBaseProj.Presentation/Components/Shared/ImageUploader.razor) | New (spec req) | `PostId` |
| [`DeleteConfirmModal.razor`](src/AspBaseProj.Presentation/Components/Shared/DeleteConfirmModal.razor) | PostDetail.razor + MyPosts.razor | `Title`, `OnConfirm`, `OnCancel` |
| [`SocialEmbed.razor`](src/AspBaseProj.Presentation/Components/Shared/SocialEmbed.razor) | New (spec req) | `Url` |

---

## Refactoring 6: Fix SPA Auth Flow (Remove Form-Post Redirects)

**Severity:** 🟠 High  
**Effort:** Medium  
**Files affected:** [`Login.razor`](src/AspBaseProj.Presentation/Components/Pages/Login.razor:16), [`Register.razor`](src/AspBaseProj.Presentation/Components/Pages/Register.razor:16), [`MainLayout.razor`](src/AspBaseProj.Presentation/Components/Layout/MainLayout.razor:36)

### Problem

The spec says: *"The frontend is a SPA with client-side routing — no full page round-trips."*  
But Login/Register use **traditional HTML form posts** (`<form method="post" action="/api/auth/...">`) causing full page reloads. Logout is also a form post. This:

- Breaks SPA behavior
- Loses Blazor component state
- Causes flickering full page reloads

### Solution

Replace form posts with **AJAX calls + client-side navigation**:

1. Login.razor: Use `@onclick` handler calling `Api.LoginAsync()` (or direct service after Refactoring 2), then `Navigation.NavigateTo("/", forceLoad: false)`
2. Register.razor: Same pattern
3. Logout button in MainLayout: Call logout API via AJAX, then navigate
4. All auth actions become **SPA-friendly** without page reload

---

## Refactoring 7: Add CascadingAuthState Component

**Severity:** 🟡 Medium  
**Effort:** Small  
**Files affected:** [`MainLayout.razor`](src/AspBaseProj.Presentation/Components/Layout/MainLayout.razor:108), all pages

### Problem

Every page and the layout individually call `Api.GetCurrentUserAsync()` to check auth state. This causes:
- N+1 user info requests
- No shared, reactive auth state
- The layout cannot pass auth info to pages

### Solution

Create a `CascadingAuthState` component that:

1. Fetches user info **once** at app startup
2. Exposes as `CascadingParameter` to all child components
3. Reactively updates on login/logout via an event/callback

```razor
<!-- App.razor -->
<CascadingAuthState>
    <Routes @rendermode="InteractiveServer" />
</CascadingAuthState>
```

```csharp
public class CascadingAuthState : ComponentBase
{
    [Inject] private CurrentUserService CurrentUser { get; set; }
    
    public AuthUserInfo? UserInfo { get; private set; }
    public bool IsAuthenticated => UserInfo is not null;
    
    protected override async Task OnInitializedAsync() { /* load user info */ }
    
    public async Task RefreshAsync() { /* re-fetch */ }
}
```

All pages and MainLayout then receive `CascadingParameter` auth state instead of calling the API themselves.

---

## Refactoring 8: Depth Limit for Nested Comments + Visual Tree-Lines

**Severity:** 🟡 Medium  
**Effort:** Small  
**Files affected:** [`CommentNode.razor`](src/AspBaseProj.Presentation/Components/Shared/CommentNode.razor:1), [`CommentSection.razor`](src/AspBaseProj.Presentation/Components/Shared/CommentSection.razor:1), CSS

### Problem

1. No depth limit — nested comments could theoretically recurse infinitely
2. The UI spec says *"visual tree-lines for nesting hierarchy"* but there are none
3. Guest pending-approval comments still show "Reply" button incorrectly

### Solution

1. Add `MaxDepth` parameter (e.g., 5) to `CommentNode`, stop rendering replies beyond it
2. Show "Replies are closed for this thread" message at max depth
3. Add CSS `::before` pseudo-elements to draw tree-lines
4. Guest comments that are not yet approved should not show "Reply" button

---

## Refactoring 9: Remove Repository Over-Abstraction

**Severity:** 🟡 Medium  
**Effort:** Large  
**Files affected:** All repositories, interfaces, controllers, pages

### Problem

Every entity has a full repository interface + implementation with identical CRUD methods (GetById, Add, Update, Delete). EF Core `DbSet<T>` already provides `FindAsync`, `AddAsync`, `Remove`, `SaveChangesAsync`. This adds:

- ~800 lines of boilerplate code
- No clear benefit (no unit-of-work abstraction needed)
- Maintenance burden for every new entity

### Solution

Option A (recommended): **Inject `BlogDbContext` directly** in controllers and services, using EF Core as the repository (it IS the repository + unit of work pattern).

Option B (conservative): **Simplify repositories to only custom query methods**, removing standard CRUD:

```
ICommentRepository
├── GetByPostIdAsync(postId)              // keeps custom query
├── GetPendingAsync()                      // keeps custom query
├── GetCommentCountsByPostIdsAsync(ids)   // keeps custom query
└── [Remove: GetById, Add, Update, Delete] // use DbContext directly
```

---

## Refactoring 10: Improve Error Handling & User Feedback

**Severity:** 🟡 Medium  
**Effort:** Medium  
**Files affected:** All pages, [`Program.cs`](src/AspBaseProj.Presentation/Program.cs:1)

### Problem

- No global error handling (Blazor circuit errors crash the app)
- No `ErrorBoundary` component
- Inconsistent error display (string field in each page vs. shared component)
- API errors are swallowed (`.IsSuccessStatusCode` check returns null, UI shows nothing)

### Solution

1. Add `<ErrorBoundary>` around routes in [`App.razor`](src/AspBaseProj.Presentation/Components/App.razor:15)
2. Create a shared `ErrorMessage` / `SuccessMessage` component
3. Add try-catch with user-friendly messages in all API interaction methods
4. Register a custom circuit handler for Blazor Server reconnection

---

## Refactoring 11: Add ImageUploader and SocialEmbed Components

**Severity:** 🟡 Medium  
**Effort:** Medium  
**Files affected:** New files, [`PostEditor.razor`](src/AspBaseProj.Presentation/Components/Pages/PostEditor.razor:1)

### Problem

The UI spec lists `ImageUploader` and `SocialEmbed` as required components for the post editor, but they are **not implemented**. The media API endpoints exist but have no UI. Social media/video embeds have no UI or rendering.

### Solution

1. **ImageUploader**: Add a file upload section in PostEditor that:
   - Uses the existing `/api/media/post/{postId}` endpoints
   - Shows uploaded images with thumbnails
   - Allows deletion
   - Validates file type and size client-side

2. **SocialEmbed**: Create a component that:
   - Detects URLs from YouTube, Vimeo, Twitter/X, etc. in post content
   - Renders them as embedded cards (oEmbed-style)
   - Provides a UI to insert such links in the editor

---

## Refactoring 12: Fix PostDetail UI Logic Gaps

**Severity:** 🟢 Low  
**Effort:** Small  
**Files affected:** [`PostDetail.razor`](src/AspBaseProj.Presentation/Components/Pages/PostDetail.razor:1)

### Problem

1. Admin/root who are not the author of a draft post should see "Edit" button but currently only `isOwner` check is used
2. Like/dislike/bookmark buttons appear for unauthenticated users but don't work (need to be hidden or show login prompt)
3. `CommentSection` is hidden for drafts — author might want to preview comments

### Solution

1. Add `canEdit` boolean: `isOwner || isAdmin || isRoot`
2. Show only "Login to rate" instead of active buttons for unauthenticated users
3. Show `CommentSection` for drafts if the viewer is the author/admin/root

---

## Refactoring 13: Add Test Project

**Severity:** 🟢 Low  
**Effort:** Medium  
**Files affected:** New `tests/` directory

### Problem

There is no test project. The refactored controllers, services, and components should have:
- Unit tests for AuthService
- Integration tests for API endpoints
- Component tests for Blazor components

### Solution

Create test project(s):

```
tests/
├── AspBaseProj.Application.Tests/   # AuthService, business logic
├── AspBaseProj.Api.Tests/           # Controller integration tests
└── AspBaseProj.Presentation.Tests/  # Blazor component tests (bUnit)
```

---

## Priority & Dependency Map

```
Refactoring 1 (Controllers)
    └── depends on ▶ Refactoring 3 (DTOs to Application)
                      └── Refactoring 2 (Self-API elimination)
                            └── Refactoring 7 (CascadingAuthState)
                                  └── Refactoring 6 (SPA Auth)

Refactoring 4 (Markdown) — independent

Refactoring 5 (Components)
    └── depends on ▶ Refactoring 2 (Self-API elimination)
                      └── Refactoring 7 (CascadingAuthState)

Refactoring 8 (Comments depth) — independent
Refactoring 9 (Repository) — independent, but high impact
Refactoring 10 (Error handling) — independent
Refactoring 11 (ImageUploader + SocialEmbed) — independent
Refactoring 12 (PostDetail UI) — independent
Refactoring 13 (Tests) — after all code stability
```

### Recommended Execution Order

| Phase | Refactorings | Goal |
|-------|-------------|------|
| **Phase 1** | 3 → 1 → 2 → 7 | Core architecture fix: DTOs → Controllers → Remove self-API → Shared auth |
| **Phase 2** | 6 → 5 → 12 | SPA auth flow → Extract components → Fix UI gaps |
| **Phase 3** | 4 → 8 → 11 | Markdown → Comments depth → Missing features |
| **Phase 4** | 9 → 10 | Simplify repos → Error handling |
| **Phase 5** | 13 | Tests |

---

## Effort Estimation

| Refactoring | Estimated Effort | Risk |
|-------------|-----------------|------|
| 1. Controllers | 2-3 days | Medium (breaking changes) |
| 2. Self-API | 1-2 days | Medium (many files) |
| 3. DTOs | 0.5 days | Low |
| 4. Markdown | 0.5 days | Low |
| 5. Components | 2-3 days | Low |
| 6. SPA Auth | 1 day | Medium |
| 7. CascadingAuthState | 0.5 days | Low |
| 8. Comments depth | 0.5 days | Low |
| 9. Repository | 1-2 days | Medium |
| 10. Error handling | 1 day | Low |
| 11. ImageUploader+Social | 2 days | Medium |
| 12. PostDetail UI | 0.5 days | Low |
| 13. Tests | 3-4 days | Low |

**Total estimated effort: ~15-20 days**

---

## Conclusion

The project has a solid Clean Architecture foundation and a well-thought-out database schema. The **most critical issues** are:

1. **Inline controllers in Program.cs** — makes the codebase unmaintainable
2. **Self-API anti-pattern** — adds unnecessary complexity and overhead
3. **DTOs in wrong layer** — violates architectural boundaries
4. **Primitive markdown rendering** — produces poor user experience
5. **Missing specified components** — the UI doesn't match the specification

Addressing these 5 items (Phase 1 + Phase 3) will bring the project from a **functional prototype** to a **production-ready application**. The remaining items are important but can follow iteratively.