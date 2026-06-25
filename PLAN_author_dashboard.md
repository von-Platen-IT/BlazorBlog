# Author Dashboard — Planungsdokument

> **Status:** ENTWURF — wartet auf Abnahme  
> **Erstellt:** 2026-06-22  
> **Ziel:** Übersichtsseite für Autoren mit allen eigenen Beiträgen, Kommentar-Interaktionen und Draft-Verwaltung

---

## 1. Problemstellung

### Aktueller Zustand (IST)

1. Ein Author erstellt einen Post über [`/posts/new`](src/AspBaseProj.Presentation/Components/Pages/PostEditor.razor). Der Toggle-Schalter „Published" ist standardmäßig auf `false`.
2. Wenn der Author auf **Create** klickt, ohne vorher „Published" zu aktivieren, wird der Post mit `IsPublished = false` in der Datenbank gespeichert.
3. Der [`PostEditor`](src/AspBaseProj.Presentation/Components/Pages/PostEditor.razor:83) navigiert nach dem Erstellen zu `/posts/{result.Id}` — aber die [`GET /api/posts/{id}`](src/AspBaseProj.Presentation/Program.cs:252) Route prüft auf `post.IsPublished` und gibt `404` zurück, wenn der Post nicht veröffentlicht ist.
4. Die [Home-Seite](src/AspBaseProj.Presentation/Components/Pages/Home.razor) (`/`) zeigt nur veröffentlichte Posts an (via [`GetPublishedAsync`](src/AspBaseProj.Infrastructure/Data/Repositories/PostRepository.cs:13)).
5. Es existiert **keine** Seite oder Navigation, auf der ein Author seine eigenen (unveröffentlichten) Posts einsehen, bearbeiten oder nachträglich veröffentlichen kann.
6. Der Author verliert nach dem Erstellen eines Drafts den Zugriff auf seinen eigenen Beitrag — ein Dead End.

### Gewünschter Zustand (SOLL)

Ein Author hat eine zentrale Übersicht („My Posts" / „Meine Beiträge"), die:
- **Alle eigenen Posts** auflistet — sowohl veröffentlichte als auch Drafts
- **Sofortigen Zugriff** auf Edit, Delete und nachträgliches Publish bietet
- **Kommentar-Interaktionen** übersichtlich darstellt (Anzahl Kommentare pro Post, neueste Kommentare)
- **Status-Indikatoren** zeigt (Draft, Published, Kommentar-Aktivität)
- Eine **ergonomische, komfortable Arbeitsumgebung** für den Author schafft

---

## 2. Routen-Design

| Route | Seite | Beschreibung |
|-------|-------|-------------|
| `/my/posts` | `MyPosts.razor` | **NEU** — Zentrale Author-Übersicht über alle eigenen Posts |
| `/my/posts/:id/comments` | `MyPostComments.razor` | **NEU** — Detailansicht aller Kommentare zu einem eigenen Post (optional, Phase 2) |

> **Hinweis:** Die bestehende `/profile`-Route bleibt unverändert. `/my/posts` ist eine separate, autor-zentrierte Arbeitsfläche.

---

## 3. API-Endpunkte

### 3.1 Neuer Endpunkt: `GET /api/posts/my`

**Autorisierung:** `AuthorOrAdminOrRootPolicy` (alle Rollen, die Posts erstellen dürfen)

**Anfrage-Parameter:**

| Parameter | Typ | Default | Beschreibung |
|-----------|-----|---------|-------------|
| `page` | `int` | `1` | Seitennummer |
| `pageSize` | `int` | `10` | Einträge pro Seite |

**Antwort:**

```json
{
  "posts": [
    {
      "id": "guid",
      "title": "string",
      "content": "string (truncated/excerpt, first 200 chars)",
      "isPublished": true,
      "createdAt": "datetime",
      "updatedAt": "datetime | null",
      "publishedAt": "datetime | null",
      "commentCount": 5,
      "latestComment": {
        "id": "guid",
        "content": "string (truncated)",
        "authorName": "string",
        "createdAt": "datetime"
      } | null
    }
  ],
  "total": 42,
  "page": 1,
  "pageSize": 10
}
```

**Wichtige Design-Entscheidungen:**
- `content` wird auf ~200 Zeichen gekürzt (Excerpt für die Listenansicht), damit die Tabelle performant und übersichtlich bleibt
- `commentCount` zählt alle **approved** Kommentare (Top-Level + Replies) für diesen Post
- `latestComment` zeigt den neuesten approved Kommentar als Vorschau (null wenn keine Kommentare vorhanden)
- Sortierung: `CreatedAt DESC` (neueste Posts zuerst)

### 3.2 Repository-Erweiterungen

#### `ICommentRepository` — Neue Methode

```csharp
// In: src/AspBaseProj.Domain/Interfaces/ICommentRepository.cs
Task<Dictionary<Guid, int>> GetCommentCountsByPostIdsAsync(List<Guid> postIds, CancellationToken ct = default);
```

#### `CommentRepository` — Implementierung

```csharp
// In: src/AspBaseProj.Infrastructure/Data/Repositories/CommentRepository.cs
public async Task<Dictionary<Guid, int>> GetCommentCountsByPostIdsAsync(List<Guid> postIds, CancellationToken ct = default)
{
    return await db.Comments
        .Where(c => postIds.Contains(c.PostId) && c.IsApproved)
        .GroupBy(c => c.PostId)
        .Select(g => new { PostId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.PostId, x => x.Count, ct);
}
```

#### `IPostRepository` — Erweiterung (optional)

Die bestehende Methode [`GetByAuthorIdAsync`](src/AspBaseProj.Domain/Interfaces/IPostRepository.cs:9) ist bereits vorhanden, muss aber um Paginierung erweitert werden:

```csharp
// In: src/AspBaseProj.Domain/Interfaces/IPostRepository.cs
Task<(List<Post> Posts, int TotalCount)> GetByAuthorIdPaginatedAsync(Guid authorId, int page, int pageSize, CancellationToken ct = default);
```

---

## 4. UI-Design — MyPosts.razor

### 4.1 Layout (Wireframe)

```
┌─────────────────────────────────────────────────────────┐
│  📝 Meine Beiträge                        [+ New Post]  │
│                                                         │
│  [Filter: Alle | Published | Drafts]  [Suche... 🔍]     │
│                                                         │
│ ┌─────────────────────────────────────────────────────┐  │
│ │ Title              │ Status    │ Comments │ Actions │  │
│ ├─────────────────────────────────────────────────────┤  │
│ │ Mein erster Post   │ ✅ Publ.  │ 💬 12   │ ✏️ 🗑️  │  │
│ │ Draft über Kafka   │ 📝 Draft  │ —       │ ✏️ 🗑️ ✅ │  │
│ │ Blazor Tipps       │ ✅ Publ.  │ 💬 3    │ ✏️ 🗑️  │  │
│ │ ...                │ ...       │ ...     │ ...     │  │
│ └─────────────────────────────────────────────────────┘  │
│                                                         │
│  « 1  2  3  4  5 »                                      │
└─────────────────────────────────────────────────────────┘
```

### 4.2 Komponenten-Beschreibung

| Komponente | Typ | Beschreibung |
|------------|-----|-------------|
| `PostTable` | `table` | Responsive Tabelle aller eigenen Posts mit Spalten: Title, Status, Comments, Actions |
| `StatusBadge` | `badge` | Farbiger Badge: Grün = Published, Gelb/Orange = Draft |
| `FilterBar` | `navigation` | Filter-Buttons: Alle / Published / Drafts |
| `Pagination` | `navigation` | Seiten-Navigation (wie auf Home-Seite) |
| `ActionButtons` | `actions` | Pro Zeile: Edit (✏️), Delete (🗑️), Publish (✅, nur bei Drafts) |

### 4.3 Responsive Design

- **Desktop (≥992px):** Volle Tabelle mit allen Spalten
- **Tablet (≥768px):** Tabelle ohne `latestComment`-Vorschau, Actions als Icon-Buttons
- **Smartphone (<768px):** Karten-Layout statt Tabelle (Card pro Post), Filter als Dropdown

### 4.4 User Actions

| Aktion | Beschreibung | Berechtigung |
|--------|-------------|-------------|
| **Neuen Post erstellen** | Button „+ New Post" → navigiert zu `/posts/new` | Author, Admin, Root |
| **Post bearbeiten** | ✏️ Icon → navigiert zu `/posts/{id}/edit` | Eigener Post (Author), alle (Admin/Root) |
| **Post löschen** | 🗑️ Icon → Bestätigungsdialog → DELETE | Eigener Post (Author), alle (Admin/Root) |
| **Draft veröffentlichen** | ✅ Publish-Button → setzt `IsPublished=true` via PUT | Eigener Post (Author), alle (Admin/Root) |
| **Kommentare ansehen** | Klick auf Kommentar-Count → öffnet `/posts/{id}` mit Fokus auf Kommentare | Alle (eigene Posts) |
| **Filtern** | Tabs: Alle / Published / Drafts | — |
| **Paginieren** | Seiten-Navigation | — |

---

## 5. Datenfluss

```
┌──────────────┐     GET /api/posts/my      ┌──────────────┐
│  MyPosts.razor │ ──────────────────────────▶ │  Program.cs   │
│  (Blazor SSR)  │ ◀────────────────────────── │  (API Route)  │
└──────────────┘     JSON Response            └──────┬───────┘
                                                     │
                                                     ▼
                                            ┌─────────────────┐
                                            │ CurrentUserService│
                                            │ (UserId ermitteln)│
                                            └────────┬────────┘
                                                     │
                                                     ▼
                                        ┌───────────────────────┐
                                        │ IPostRepository        │
                                        │ GetByAuthorIdPaginated │
                                        └───────────┬───────────┘
                                                    │
                                                    ▼
                                        ┌───────────────────────┐
                                        │ ICommentRepository     │
                                        │ GetCommentCountsByPost │
                                        │ IdsAsync               │
                                        └───────────────────────┘
```

### Ablauf:

1. `MyPosts.razor` ruft `ApiClient.GetMyPostsAsync(page, pageSize, filter)` auf
2. `ApiClient` sendet `GET /api/posts/my?page=1&pageSize=10&filter=all`
3. API-Endpunkt ermittelt `userId` aus `CurrentUserService`
4. Repository lädt paginierte Posts des Authors
5. Repository lädt Comment-Counts für diese Posts in einem Batch-Query
6. Response wird als JSON mit Posts + Counts + latestComment zurückgegeben
7. UI rendert die Tabelle mit Status-Badges und Action-Buttons

---

## 6. Navigation-Änderungen

### [`MainLayout.razor`](src/AspBaseProj.Presentation/Components/Layout/MainLayout.razor) — Ergänzung

```razor
@* Nach dem "New Post" Link, Zeile 18 *@
@if (IsAuthenticated && (IsAuthor || IsAdmin || IsRoot))
{
    <li class="nav-item"><NavLink class="nav-link" href="/my/posts">📋 My Posts</NavLink></li>
    <li class="nav-item"><NavLink class="nav-link" href="/posts/new">✏️ New Post</NavLink></li>
}
```

### Footer-Navigation — Ergänzung

```razor
@* Unter "Account" im Footer *@
<li class="mb-1"><a href="/my/posts" class="text-light text-decoration-none">My Posts</a></li>
```

---

## 7. Datei-Änderungen (Schritt-für-Schritt)

### Phase 1: Backend (API + Repository)

| # | Datei | Änderung |
|---|-------|----------|
| 1 | [`ICommentRepository.cs`](src/AspBaseProj.Domain/Interfaces/ICommentRepository.cs) | Neue Methode `GetCommentCountsByPostIdsAsync` |
| 2 | [`CommentRepository.cs`](src/AspBaseProj.Infrastructure/Data/Repositories/CommentRepository.cs) | Implementierung der neuen Methode |
| 3 | [`IPostRepository.cs`](src/AspBaseProj.Domain/Interfaces/IPostRepository.cs) | Neue Methode `GetByAuthorIdPaginatedAsync` |
| 4 | [`PostRepository.cs`](src/AspBaseProj.Infrastructure/Data/Repositories/PostRepository.cs) | Implementierung der neuen Methode |
| 5 | [`Program.cs`](src/AspBaseProj.Presentation/Program.cs) | Neuer Endpunkt `GET /api/posts/my` (nach Zeile ~253) |

### Phase 2: Frontend (ApiClient + UI)

| # | Datei | Änderung |
|---|-------|----------|
| 6 | [`ApiClient.cs`](src/AspBaseProj.Presentation/Components/Shared/ApiClient.cs) | Neue Methode `GetMyPostsAsync`, neue DTOs `MyPostDto`, `MyPostsResponse` |
| 7 | [`MyPosts.razor`](src/AspBaseProj.Presentation/Components/Pages/MyPosts.razor) | **NEUE DATEI** — Die Author-Übersichtsseite |
| 8 | [`MainLayout.razor`](src/AspBaseProj.Presentation/Components/Layout/MainLayout.razor) | Nav-Link „My Posts" hinzufügen |
| 9 | [`PostEditor.razor`](src/AspBaseProj.Presentation/Components/Pages/PostEditor.razor) | Redirect nach Draft-Erstellung zu `/my/posts` statt `/posts/{id}` |
| 10 | [`PostDetail.razor`](src/AspBaseProj.Presentation/Components/Pages/PostDetail.razor) | Zugriff auf eigene unveröffentlichte Posts erlauben (Owner-Check) |

### Phase 3: UI-Dokumentation aktualisieren

| # | Datei | Änderung |
|---|-------|----------|
| 11 | [`.roo/rules/ui-specification.md`](.roo/rules/ui-specification.md) | Neue Seite „My Posts" dokumentieren |

---

## 8. Offene Fragen / Entscheidungen

### 8.1 PostDetail-Zugriff auf Drafts

**Problem:** Aktuell gibt [`GET /api/posts/{id}`](src/AspBaseProj.Presentation/Program.cs:252) nur veröffentlichte Posts zurück. Ein Author, der `/posts/{draft-id}` aufruft, bekommt 404.

**Lösungsvorschlag:** Den Endpunkt so ändern, dass er den Post auch dann zurückgibt, wenn:
- Der Post nicht veröffentlicht ist, ABER
- Der anfragende Benutzer der Author des Posts ist (oder Admin/Root)

```csharp
// Geändert in Program.cs, Zeile ~252:
postsGroup.MapGet("/{id:guid}", async (Guid id, IPostRepository repo, CurrentUserService user) =>
{
    var post = await repo.GetByIdAsync(id);
    if (post is null) return Results.NotFound();
    // Erlaube Zugriff wenn published ODER wenn current user der Author/Admin/Root ist
    if (!post.IsPublished && !user.IsAuthenticated) return Results.NotFound();
    if (!post.IsPublished && post.AuthorId != user.UserId && !user.IsRoot && !user.IsInGroup("Admin"))
        return Results.NotFound();
    return Results.Ok(MapPost(post));
});
```

### 8.2 Redirect nach Draft-Erstellung

**Aktuell:** [`PostEditor.razor`](src/AspBaseProj.Presentation/Components/Pages/PostEditor.razor:83) navigiert immer zu `/posts/{result.Id}`.

**Vorschlag:** Wenn `isPublished == false` → navigiere zu `/my/posts` mit einer Success-Meldung („Draft gespeichert"). Wenn `isPublished == true` → navigiere zu `/posts/{id}` (wie bisher).

### 8.3 Publish-Button in der MyPosts-Tabelle

Ein eigener „Publish"-Button (✅) in der Tabellenzeile für Drafts, der via `PUT /api/posts/{id}` mit `isPublished: true` den Post veröffentlicht, ohne die Edit-Seite öffnen zu müssen. Spart Klicks und ist ergonomischer.

### 8.4 Bewertungssystem (Rating)

Das Bewertungssystem (Likes/Dislikes, Upvotes/Downvotes) wird in einem **separaten Planungsdokument** behandelt und ist nicht Teil dieser Implementierung. Die vorgeschlagene Architektur (z.B. `PostRating`-Entität mit `PostId`, `UserId`, `IsPositive`) wird später integriert. Die `MyPosts`-Seite wird dann um eine „Rating"-Spalte erweitert.

---

## 9. Zusammenfassung der zu erstellenden / ändernden Dateien

| Aktion | Datei |
|--------|-------|
| **ÄNDERN** | [`ICommentRepository.cs`](src/AspBaseProj.Domain/Interfaces/ICommentRepository.cs) |
| **ÄNDERN** | [`CommentRepository.cs`](src/AspBaseProj.Infrastructure/Data/Repositories/CommentRepository.cs) |
| **ÄNDERN** | [`IPostRepository.cs`](src/AspBaseProj.Domain/Interfaces/IPostRepository.cs) |
| **ÄNDERN** | [`PostRepository.cs`](src/AspBaseProj.Infrastructure/Data/Repositories/PostRepository.cs) |
| **ÄNDERN** | [`Program.cs`](src/AspBaseProj.Presentation/Program.cs) |
| **ÄNDERN** | [`ApiClient.cs`](src/AspBaseProj.Presentation/Components/Shared/ApiClient.cs) |
| **NEU** | [`MyPosts.razor`](src/AspBaseProj.Presentation/Components/Pages/MyPosts.razor) |
| **ÄNDERN** | [`MainLayout.razor`](src/AspBaseProj.Presentation/Components/Layout/MainLayout.razor) |
| **ÄNDERN** | [`PostEditor.razor`](src/AspBaseProj.Presentation/Components/Pages/PostEditor.razor) |
| **ÄNDERN** | [`PostDetail.razor`](src/AspBaseProj.Presentation/Components/Pages/PostDetail.razor) |
| **ÄNDERN** | [`.roo/rules/ui-specification.md`](.roo/rules/ui-specification.md) |

---

## 10. Nächste Schritte

1. ✅ Planungsdokument prüfen und abnehmen
2. ⬜ Feedback einarbeiten
3. ⬜ Phase 1: Backend-Implementierung (Repository + API)
4. ⬜ Phase 2: Frontend-Implementierung (ApiClient + UI + Navigation)
5. ⬜ Phase 3: UI-Spec aktualisieren
6. ⬜ Testen: Draft erstellen → in My Posts erscheinen → Publish → auf Home sichtbar
