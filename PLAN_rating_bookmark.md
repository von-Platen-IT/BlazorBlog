# Bewertungs- & Bookmark-System — Planungsdokument

> **Status:** ENTWURF — wartet auf Abnahme  
> **Erstellt:** 2026-06-22  
> **Ziel:** Like/Dislike-Bewertung und Bookmark-Funktion für authentifizierte Benutzer

---

## 1. Anforderungsanalyse

### 1.1 Bewertungssystem (Like/Dislike)

| Anforderung | Beschreibung |
|-------------|-------------|
| **Like** | Authentifizierte Benutzer können einen Post liken (👍 grün) |
| **Dislike** | Authentifizierte Benutzer können einen Post disliken (👎 rot) |
| **Toggle** | Erneutes Klicken auf Like/Dislike entfernt die Bewertung |
| **Mutual Exclusion** | Ein Like entfernt ein bestehendes Dislike (und umgekehrt) — ein Benutzer kann nicht gleichzeitig liken und disliken |
| **Counts sichtbar** | Alle Besucher (auch nicht authentifiziert) sehen die Gesamtanzahl Likes/Dislikes neben den Icons |
| **Eigene Bewertung** | Der authentifizierte Benutzer sieht seinen eigenen Bewertungsstatus (hervorgehobenes Icon) |

### 1.2 Bookmark-System

| Anforderung | Beschreibung |
|-------------|-------------|
| **Bookmark** | Authentifizierte Benutzer können einen Post mit Lesezeichen (🔖) versehen |
| **Toggle** | Erneutes Klicken entfernt das Lesezeichen |
| **Bookmark-Übersicht** | Im Bereich „My Posts" gibt es einen Tab „🔖 Bookmarks", der alle gespeicherten Beiträge auflistet |
| **Counts sichtbar** | Das Bookmark-Icon zeigt den Status (ausgefüllt = gespeichert, Umriss = nicht gespeichert) |

---

## 2. Datenbank-Design

### 2.1 Neue Entität: `PostRating`

```yaml
PostRating:
  Id:          Guid        PK
  PostId:      Guid        FK → Post, NOT NULL
  UserId:      Guid        FK → AppUser, NOT NULL
  IsLike:      bool        NOT NULL  (true = Like, false = Dislike)
  CreatedAt:   DateTime    NOT NULL

Indexes:
  - UNIQUE (PostId, UserId)  — ein Benutzer kann pro Post nur eine Bewertung haben
  - (PostId)                 — für Count-Queries
```

### 2.2 Neue Entität: `Bookmark`

```yaml
Bookmark:
  Id:          Guid        PK
  PostId:      Guid        FK → Post, NOT NULL
  UserId:      Guid        FK → AppUser, NOT NULL
  CreatedAt:   DateTime    NOT NULL

Indexes:
  - UNIQUE (PostId, UserId)  — ein Benutzer kann einen Post nur einmal bookmarken
  - (UserId)                 — für "Meine Bookmarks"-Query
```

### 2.3 Navigation Properties (Post-Erweiterung)

[`Post.cs`](src/AspBaseProj.Domain/Entities/Post.cs) erhält zwei neue Navigation Properties:

```csharp
public ICollection<PostRating> Ratings { get; set; } = new List<PostRating>();
public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
```

---

## 3. API-Endpunkte

### 3.1 Ratings

| Methode | Route | Auth | Beschreibung |
|---------|-------|------|-------------|
| `POST` | `/api/posts/{id}/like` | ✅ Auth | Toggle Like (entfernt ggf. Dislike) |
| `POST` | `/api/posts/{id}/dislike` | ✅ Auth | Toggle Dislike (entfernt ggf. Like) |
| `GET` | `/api/posts/{id}/rating` | Optional | Gibt `{ likeCount, dislikeCount, userRating: null/"like"/"dislike" }` zurück |

**`POST /api/posts/{id}/like` — Logik:**

```
1. Prüfe ob Post existiert → sonst 404
2. Prüfe ob User authentifiziert → sonst 401
3. Suche bestehendes Rating des Users für diesen Post
4. Wenn Rating existiert UND IsLike == true:
   → Lösche Rating (Toggle aus) → return { likeCount, dislikeCount, userRating: null }
5. Wenn Rating existiert UND IsLike == false:
   → Ändere IsLike auf true (Wechsel von Dislike zu Like) → return { likeCount, dislikeCount, userRating: "like" }
6. Wenn kein Rating existiert:
   → Erstelle neues Rating mit IsLike = true → return { likeCount, dislikeCount, userRating: "like" }
```

**`POST /api/posts/{id}/dislike` — Logik:** Analog, mit `IsLike = false`.

### 3.2 Bookmarks

| Methode | Route | Auth | Beschreibung |
|---------|-------|------|-------------|
| `POST` | `/api/posts/{id}/bookmark` | ✅ Auth | Toggle Bookmark |
| `GET` | `/api/posts/{id}/bookmark` | ✅ Auth | Gibt `{ isBookmarked: bool }` zurück |
| `GET` | `/api/posts/bookmarks` | ✅ Auth | Gibt paginierte Liste der bookmarked Posts zurück |

**`POST /api/posts/{id}/bookmark` — Logik:**

```
1. Prüfe ob Post existiert → sonst 404
2. Prüfe ob User authentifiziert → sonst 401
3. Suche bestehendes Bookmark des Users für diesen Post
4. Wenn Bookmark existiert:
   → Lösche Bookmark → return { isBookmarked: false }
5. Wenn kein Bookmark existiert:
   → Erstelle Bookmark → return { isBookmarked: true }
```

**`GET /api/posts/bookmarks` — Response:**

```json
{
  "posts": [
    {
      "id": "guid",
      "title": "string",
      "content": "string (excerpt)",
      "authorName": "string",
      "isPublished": true,
      "publishedAt": "datetime",
      "likeCount": 5,
      "dislikeCount": 1,
      "commentCount": 3,
      "bookmarkedAt": "datetime"
    }
  ],
  "total": 10,
  "page": 1,
  "pageSize": 10
}
```

---

## 4. Repository-Änderungen

### 4.1 Neue Interfaces

```csharp
// src/AspBaseProj.Domain/Interfaces/IPostRatingRepository.cs
public interface IPostRatingRepository
{
    Task<PostRating?> GetByPostAndUserAsync(Guid postId, Guid userId, CancellationToken ct = default);
    Task<(int LikeCount, int DislikeCount)> GetCountsAsync(Guid postId, CancellationToken ct = default);
    Task<Dictionary<Guid, (int LikeCount, int DislikeCount)>> GetCountsByPostIdsAsync(List<Guid> postIds, CancellationToken ct = default);
    Task<PostRating> AddAsync(PostRating rating, CancellationToken ct = default);
    Task UpdateAsync(PostRating rating, CancellationToken ct = default);
    Task DeleteAsync(PostRating rating, CancellationToken ct = default);
}
```

```csharp
// src/AspBaseProj.Domain/Interfaces/IBookmarkRepository.cs
public interface IBookmarkRepository
{
    Task<Bookmark?> GetByPostAndUserAsync(Guid postId, Guid userId, CancellationToken ct = default);
    Task<(List<Post> Posts, int TotalCount)> GetBookmarkedPostsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<Bookmark> AddAsync(Bookmark bookmark, CancellationToken ct = default);
    Task DeleteAsync(Bookmark bookmark, CancellationToken ct = default);
}
```

### 4.2 Neue Repository-Implementierungen

```csharp
// src/AspBaseProj.Infrastructure/Data/Repositories/PostRatingRepository.cs
// src/AspBaseProj.Infrastructure/Data/Repositories/BookmarkRepository.cs
```

### 4.3 DependencyInjection

[`DependencyInjection.cs`](src/AspBaseProj.Infrastructure/DependencyInjection.cs) muss um die neuen Repositories erweitert werden.

---

## 5. UI-Änderungen

### 5.1 PostDetail.razor — Like/Dislike/Bookmark Bar

**Position:** Zwischen dem Post-Inhalt und dem Kommentar-Bereich.

```
┌─────────────────────────────────────────────────────────┐
│  👍 12    👎 3    🔖 Bookmark                           │
│  (grün)   (rot)   (ausgefüllt wenn gespeichert)         │
└─────────────────────────────────────────────────────────┘
```

- **Nicht authentifiziert:** Icons werden angezeigt, Counts sichtbar, aber Klick zeigt Login-Hinweis
- **Authentifiziert:** Eigene Bewertung wird hervorgehoben (gefülltes Icon), Klick toggled

### 5.2 Home.razor — Like/Dislike Counts auf Post-Karten

Jede Post-Karte zeigt unter dem Excerpt:

```
By AuthorName on 2026-06-22
👍 12  👎 3  💬 5
```

### 5.3 MyPosts.razor — Bookmarks Tab

Neuer Tab in der Filter-Leiste:

```
[All] [✅ Published] [📝 Drafts] [🔖 Bookmarks]
```

Der Bookmarks-Tab lädt die bookmarked Posts via `GET /api/posts/bookmarks`.

### 5.4 ApiClient.cs — Neue Methoden

```csharp
// Ratings
Task<RatingResponse?> LikePostAsync(Guid postId);
Task<RatingResponse?> DislikePostAsync(Guid postId);
Task<RatingResponse?> GetPostRatingAsync(Guid postId);

// Bookmarks
Task<BookmarkResponse?> ToggleBookmarkAsync(Guid postId);
Task<BookmarkStatusResponse?> GetBookmarkStatusAsync(Guid postId);
Task<BookmarkedPostsResponse?> GetBookmarkedPostsAsync(int page, int pageSize);
```

---

## 6. Datenbank-Migration

Eine neue EF Core Migration wird erstellt:

```
dotnet ef migrations add AddRatingsAndBookmarks
```

Diese erstellt:
- Tabelle `PostRatings` mit Unique-Index `(PostId, UserId)`
- Tabelle `Bookmarks` mit Unique-Index `(PostId, UserId)`

---

## 7. Datei-Änderungen (Schritt-für-Schritt)

### Phase 1: Domain Entities

| # | Datei | Änderung |
|---|-------|----------|
| 1 | [`PostRating.cs`](src/AspBaseProj.Domain/Entities/PostRating.cs) | **NEU** — Entity |
| 2 | [`Bookmark.cs`](src/AspBaseProj.Domain/Entities/Bookmark.cs) | **NEU** — Entity |
| 3 | [`Post.cs`](src/AspBaseProj.Domain/Entities/Post.cs) | Navigation Properties hinzufügen |

### Phase 2: Repository Interfaces

| # | Datei | Änderung |
|---|-------|----------|
| 4 | [`IPostRatingRepository.cs`](src/AspBaseProj.Domain/Interfaces/IPostRatingRepository.cs) | **NEU** |
| 5 | [`IBookmarkRepository.cs`](src/AspBaseProj.Domain/Interfaces/IBookmarkRepository.cs) | **NEU** |

### Phase 3: Infrastructure (Config + Repos + DbContext)

| # | Datei | Änderung |
|---|-------|----------|
| 6 | [`PostRatingConfiguration.cs`](src/AspBaseProj.Infrastructure/Data/Configurations/PostRatingConfiguration.cs) | **NEU** |
| 7 | [`BookmarkConfiguration.cs`](src/AspBaseProj.Infrastructure/Data/Configurations/BookmarkConfiguration.cs) | **NEU** |
| 8 | [`PostRatingRepository.cs`](src/AspBaseProj.Infrastructure/Data/Repositories/PostRatingRepository.cs) | **NEU** |
| 9 | [`BookmarkRepository.cs`](src/AspBaseProj.Infrastructure/Data/Repositories/BookmarkRepository.cs) | **NEU** |
| 10 | [`BlogDbContext.cs`](src/AspBaseProj.Infrastructure/Data/BlogDbContext.cs) | DbSets + Configurations registrieren |
| 11 | [`DependencyInjection.cs`](src/AspBaseProj.Infrastructure/DependencyInjection.cs) | Neue Repositories registrieren |

### Phase 4: API Endpoints

| # | Datei | Änderung |
|---|-------|----------|
| 12 | [`Program.cs`](src/AspBaseProj.Presentation/Program.cs) | Rating + Bookmark Endpoints |

### Phase 5: Frontend (ApiClient + UI)

| # | Datei | Änderung |
|---|-------|----------|
| 13 | [`ApiClient.cs`](src/AspBaseProj.Presentation/Components/Shared/ApiClient.cs) | Neue Methoden + DTOs |
| 14 | [`PostDetail.razor`](src/AspBaseProj.Presentation/Components/Pages/PostDetail.razor) | Like/Dislike/Bookmark Bar |
| 15 | [`Home.razor`](src/AspBaseProj.Presentation/Components/Pages/Home.razor) | Like/Dislike Counts auf Karten |
| 16 | [`MyPosts.razor`](src/AspBaseProj.Presentation/Components/Pages/MyPosts.razor) | Bookmarks Tab |

### Phase 6: Dokumentation

| # | Datei | Änderung |
|---|-------|----------|
| 17 | [`.roo/rules/database-schema.md`](.roo/rules/database-schema.md) | PostRating + Bookmark dokumentieren |
| 18 | [`.roo/rules/ui-specification.md`](.roo/rules/ui-specification.md) | Rating/Bookmark UI dokumentieren |

### Phase 7: Migration

| # | Aktion |
|---|--------|
| 19 | EF Core Migration erstellen: `AddRatingsAndBookmarks` |

---

## 8. Zusammenfassung

| Kategorie | Anzahl Dateien |
|-----------|---------------|
| **NEU** (Entities, Interfaces, Repos, Configs) | 8 |
| **ÄNDERN** (Post, DbContext, DI, Program.cs, ApiClient, UI) | 8 |
| **DOKU** (Schema, UI-Spec) | 2 |
| **MIGRATION** | 1 |
| **GESAMT** | 19 |

---

## 9. Nächste Schritte

1. ⬜ Plan abnehmen
2. ⬜ Phase 1–3: Domain + Infrastructure
3. ⬜ Phase 4: API Endpoints
4. ⬜ Phase 5: Frontend
5. ⬜ Phase 6: Dokumentation
6. ⬜ Phase 7: Migration + Build-Verifikation