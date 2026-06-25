# Interactive Server Audit & Verbesserungsplan

> **Erstellt:** 2026-06-25  
> **Ziel:** Vollständige "Desktop-App-Haptik" im Browser — keine Full-Page-Roundtrips, durchgängig Blazor Interactive Server

---

## 1. Analyse-Methodik

Jede Razor-Seite und Shared-Komponente wurde auf folgende Kriterien geprüft:

| Kriterium | Beschreibung |
|-----------|-------------|
| `@rendermode InteractiveServer` | Vorhanden auf allen interaktiven Seiten? |
| Kein `<form method="post">` | HTML-Form-POSTs verursachen Full-Page-Reloads |
| `@onclick` statt `<a href>` | Navigation soll über Blazor-Router, nicht Browser-Navigation |
| `async`/`await` in Event-Handlern | Korrekte async-Patterns für sofortige UI-Updates |
| Optimistic Updates | Statt komplettem `GET`-Reload nach Mutation |
| Kein Event-Bubbling | Direkte `ApiClient`-Injection statt Parent-Child-Event-Ketten |
| `NavigationManager.NavigateTo()` | `forceLoad: false` (Default) für client-seitiges Routing |

---

## 2. Befund pro Seite/Komponente

### 2.1 ✅ Home (`/`) — Keine Beanstandung

- `@rendermode InteractiveServer` ✅
- Paginierung per `@onclick` ✅
- Post-Navigation per `NavigationManager.NavigateTo()` ✅

> **Minor:** Paginierungs-`@onclick`-Handler könnten explizit `async () => await` sein (analog MyPosts-Fix). Kein funktionaler Bug, aber konsistent.

### 2.2 ✅ PostDetail (`/posts/{id}`) — Keine Beanstandung

- Bereits im Kommentar-Redesign vollständig umgebaut ✅
- `CommentSection`-Modul ist autark ✅

### 2.3 ✅ PostEditor (`/posts/new`, `/{id}/edit`) — Keine Beanstandung

- Interaktiver Markdown-Editor mit `@bind` ✅
- Redirect per `NavigationManager.NavigateTo()` ✅

### 2.4 ✅ MyPosts (`/my/posts`) — Keine Beanstandung

- Bereits im Async-Handler-Fix korrigiert ✅
- Filter, Paginierung, Publish, Delete alle korrekt ✅

### 2.5 🔴 Login (`/login`) — **KRITISCH: HTML-Form-POST**

```html
<!-- Aktuell: Full-Page-Roundtrip -->
<form method="post" action="/api/auth/login" data-enhance="false">
```

**Problem:** Das `<form>` sendet einen klassischen HTML-POST an den Server. Der Browser lädt die Seite komplett neu — das Gegenteil von "Desktop-App-Gefühl".

**Lösung:** Formular auf Blazor-`EditForm` oder einfaches `@onclick`-Submit umbauen. `ApiClient.LoginAsync()` aufrufen, bei Erfolg `NavigationManager.NavigateTo("/")`.

### 2.5 🔴 Register (`/register`) — **KRITISCH: HTML-Form-POST**

```html
<!-- Aktuell: Full-Page-Roundtrip -->
<form method="post" action="/api/auth/register" data-enhance="false">
```

**Problem:** Gleicher Full-Page-Roundtrip wie Login.

**Lösung:** Analog zu Login umbauen. `ApiClient.RegisterAsync()` aufrufen, bei Erfolg zu `/` navigieren.

### 2.7 🔴 MainLayout (`/`) — **KRITISCH: HTML-Form-POST für Logout**

```html
<!-- NavBar + Footer: je ein Full-Page-Roundtrip -->
<form method="post" action="/api/auth/logout" data-enhance="false">
```

**Problem:** Zwei HTML-Form-POSTs (NavBar Zeile 35, Footer Zeile 80) für Logout — beide verursachen Full-Page-Reloads.

**Lösung:** `<button @onclick="Logout">` mit `ApiClient.LogoutAsync()` + `NavigationManager.NavigateTo("/")`.

### 2.8 ⚠️ Register OAuth-Links — **Akzeptiert (OAuth-Flow)**

```html
<a href="/api/auth/login/google" class="btn btn-outline-danger">Google</a>
```

**Begründung:** OAuth 2.0 erfordert zwingend eine Redirect-basierte Authentifizierung. Der Browser muss zum Provider weiterleiten. Dies ist architekturbedingt und nicht vermeidbar. **Keine Änderung nötig.**

### 2.9 ✅ Moderation (`/admin/moderation`) — Optimierungsmöglichkeit

- `@rendermode InteractiveServer` ✅
- `ApiClient` direkt ✅
- Bulk- + Einzel-Actions korrekt ✅

> **Minor:** Nach Approve/Reject wird `comments = await Api.GetPendingCommentsAsync()` aufgerufen — vollständiger Reload. Könnte optimistisch aus der Liste entfernt werden (kein API-Call nötig). Niedrige Priorität.

### 2.10 ✅ Settings (`/admin/settings`) — Optimierungsmöglichkeit

- `@rendermode InteractiveServer` ✅

> **Minor:** Nach Save wird `settings = await Api.GetSettingsAsync()` aufgerufen. Könnte optimistisch den lokalen State updaten. Niedrige Priorität.

### 2.11 ✅ UserManagement (`/admin/users`) — Optimierungsmöglichkeit

- `@rendermode InteractiveServer` ✅

> **Minor:** Nach jeder Aktion wird `Reload()` mit zwei API-Calls aufgerufen. Könnte optimistisch lokalen State updaten. Niedrige Priorität.

### 2.12 ✅ Profile (`/profile`) — Keine Beanstandung

### 2.13 ✅ CommentSection + CommentNode — Keine Beanstandung

- Bereits im Redesign als autarkes Modul umgesetzt ✅
- Optimistic Updates ✅

---

## 3. Zusammenfassung der Befunde

| Status | Anzahl | Seiten |
|--------|--------|--------|
| 🔴 **Kritisch** (Full-Page-Roundtrip) | 3 | Login, Register, MainLayout (Logout ×2) |
| ⚠️ **Akzeptiert** (OAuth-Flow) | 3 Links | Register: Google, GitHub, Microsoft |
| 🟡 **Minor** (Optimierungs-Potential) | 4 | Home (Paginierung), Moderation, Settings, UserManagement |
| ✅ **Gut** | 7 | PostDetail, PostEditor, MyPosts, Profile, CommentSection, CommentNode |

---

## 4. Verbesserungsplan

### Phase 1: Kritische Fixes (Login, Register, Logout)

| # | Datei | Änderung |
|---|-------|----------|
| 1 | [`Login.razor`](src/AspBaseProj.Presentation/Components/Pages/Login.razor) | `<form method="post">` → Blazor `@onclick` mit `ApiClient.LoginAsync()` + `NavigationManager` |
| 2 | [`Register.razor`](src/AspBaseProj.Presentation/Components/Pages/Register.razor) | `<form method="post">` → Blazor `@onclick` mit `ApiClient.RegisterAsync()` + `NavigationManager` |
| 3 | [`MainLayout.razor`](src/AspBaseProj.Presentation/Components/Layout/MainLayout.razor) | Zwei `<form method="post" action="/api/auth/logout">` → `<button @onclick="Logout">` mit `ApiClient.LogoutAsync()` |

**Design-Prinzipien:**
- **Clean Code:** Login/Register-Logik als private async-Methoden im `@code`-Block, keine verstreute Logik im Markup
- **DRY:** Fehlerbehandlung einheitlich (try/catch → lokale `error`-Variable)
- **OOP:** `ApiClient` wird per DI injected, Seiten kennen nur ihre eigene UI-Logik

### Phase 2: Minor Optimierungen (optional, niedrige Priorität)

| # | Datei | Änderung |
|---|-------|----------|
| 4 | [`Home.razor`](src/AspBaseProj.Presentation/Components/Pages/Home.razor) | Paginierungs-Handler explizit `async () => await` |
| 5 | [`Moderation.razor`](src/AspBaseProj.Presentation/Components/Pages/Moderation.razor) | Optimistic Remove: `comments.Remove(c)` statt `GetPendingCommentsAsync()` |
| 6 | [`Settings.razor`](src/AspBaseProj.Presentation/Components/Pages/Settings.razor) | Optimistic Update: lokalen State updaten statt `GetSettingsAsync()` |
| 7 | [`UserManagement.razor`](src/AspBaseProj.Presentation/Components/Pages/UserManagement.razor) | Optimistic Update: lokalen State nach Group-Änderung updaten |

---

## 5. Code-Beispiel: Login-Umbau

### Vorher (HTML-Form-POST — Roundtrip):
```html
<form method="post" action="/api/auth/login" data-enhance="false">
    <AntiforgeryToken />
    <input name="userName" class="form-control" required />
    <input type="password" name="password" class="form-control" required />
    <button type="submit" class="btn btn-primary w-100">Login</button>
</form>
```

### Nachher (Blazor Interactive — kein Roundtrip):
```html
<div class="mb-3">
    <label class="form-label">Username</label>
    <input class="form-control" @bind="userName" required />
</div>
<div class="mb-3">
    <label class="form-label">Password</label>
    <input type="password" class="form-control" @bind="password" required />
</div>
<button class="btn btn-primary w-100"
        @onclick="async () => await LoginAsync()"
        disabled="@isLoading">
    @(isLoading ? "Logging in..." : "Login")
</button>
```

```csharp
@code {
    private string userName = "";
    private string password = "";
    private string? error;
    private bool isLoading;

    private async Task LoginAsync()
    {
        isLoading = true;
        error = null;
        var result = await Api.LoginAsync(userName, password);
        if (result is not null)
            Navigation.NavigateTo("/");
        else
            error = "Invalid credentials.";
        isLoading = false;
    }
}
```

---

## 6. Nächste Schritte

1. ⬜ Plan abnehmen
2. ⬜ Phase 1: Login, Register, MainLayout umbauen (3 Dateien)
3. ⬜ Phase 2: Minor-Optimierungen (4 Dateien, optional)
4. ⬜ Build-Verifikation