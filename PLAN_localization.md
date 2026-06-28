# Plan: Mehrsprachiges UI (i18n) für BlazorBlog

> **Ziel:** Das UI soll mehrsprachig angeboten werden — beginnend mit **Englisch** (bestehend) und **Deutsch** (neu), mit einer Dropdown-Auswahl in der Navigation. Weitere Sprachen sollen später ohne Code-Änderungen hinzufügbar sein.
>
> **Technologie:** ASP.NET Core .NET 10 Blazor Interactive Server — Verwendung der nativen `IStringLocalizer`-Infrastruktur mit `.resx`-Ressourcendateien.

---

## 1. Architektur-Ansatz: ASP.NET Core Native Localization

### Warum `IStringLocalizer` + `.resx`?

Die in ASP.NET Core / Blazor .NET 10 eingebaute Lokalisierungs-Infrastruktur basiert auf:
- **`IStringLocalizer<T>`** — typsicherer Zugriff auf übersetzte Strings
- **`.resx`-Ressourcendateien** — pro Sprache eine Datei, z.B. `Resources.en.resx`, `Resources.de.resx`
- **`CultureProvider`** — bestimmt die aktive Kultur aus Cookie / Header / URL

Dies ist der **Microsoft-Standardweg** für Blazor-Server-Apps und erfordert keine Drittanbieter-Bibliotheken.

### Kultur-Erkennung & Persistenz

```
┌──────────────────────────────────────────────────────────┐
│  1. User wählt Sprache im Dropdown (z.B. "Deutsch")      │
│  2. LanguageSelector ruft /api/culture/set?culture=de    │
│  3. Server setzt Cookie ".AspNetCore.Culture"=c=de      │
│  4. Page-Reload → RequestLocalizationMiddleware liest   │
│     Cookie und setzt Thread.CurrentThread.CurrentCulture │
│  5. IStringLocalizer<Shared> liefert deutsche Strings    │
└──────────────────────────────────────────────────────────┘
```

Die Kultur wird in einem **Cookie** gespeichert (überlebt Browser-Schließung) und vom `RequestLocalizationMiddleware` bei jedem Request ausgewertet. Dies ist der empfohlene Ansatz für Blazor Interactive Server, da die Renderung serverseitig erfolgt und die Kultur pro Circuit gesetzt werden muss.

---

## 2. Datei-Struktur

### 2.1 Ressourcendateien (`.resx`)

Alle Ressourcendateien werden im Presentation-Projekt unter `Resources/` abgelegt:

```
src/AspBaseProj.Presentation/
├── Resources/
│   ├── SharedResource.cs          ← Marker-Klasse für IStringLocalizer<SharedResource>
│   ├── SharedResource.en.resx     ← Englisch (Fallback / Default)
│   ├── SharedResource.de.resx     ← Deutsch
│   └── SharedResource.resx        ← Neutral (identisch mit en, Fallback)
```

**`SharedResource.cs`** ist eine leere Marker-Klasse — sie existiert nur, damit `IStringLocalizer<SharedResource>` einen Typ-Parameter hat:

```csharp
namespace AspBaseProj.Presentation.Resources;

public class SharedResource { }
```

### 2.2 Warum eine gemeinsame `SharedResource`?

Statt pro Page/Component eine eigene Ressourcen-Datei (z.B. `Login.razor.en.resx`), verwenden wir **eine zentrale Ressourcen-Datei** mit logischen Schlüsseln. Vorteile:
- Weniger Dateien, einfacher zu pflegen
- Strings die auf mehreren Seiten vorkommen (z.B. "Loading...", "Cancel") werden nur einmal definiert
- Schlüssel sind sprechend: `Nav_Home`, `Login_Title`, `PostDetail_LikeButton`, etc.

---

## 3. Konfiguration in `Program.cs`

Folgende Erweiterungen müssen in [`Program.cs`](src/AspBaseProj.Presentation/Program.cs:1) hinzugefügt werden:

### 3.1 Services registrieren (vor `builder.Build()`)

```csharp
// === Localization ===
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en"),   // Englisch (Default)
        new CultureInfo("de"),   // Deutsch
        // Weitere Sprachen hier hinzufügen: new CultureInfo("fr"), etc.
    };
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});
```

### 3.2 Middleware-Pipeline (nach `app.UseRouting()` / vor `app.MapRazorComponents()`)

```csharp
app.UseRequestLocalization();
```

**Wichtig:** Die Reihenfolge in der Pipeline muss sein:
```
UseAuthentication → UseAuthorization → UseStaticFiles → UseRequestLocalization → UseAntiforgery → MapControllers → MapRazorComponents
```

### 3.3 Blazor-Service-Registrierung ergänzen

```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        // Erlaubt Kultur-Wechsel im laufenden Circuit
    });
```

---

## 4. LanguageSelector-Komponente

Neue Komponente: [`Components/Shared/LanguageSelector.razor`](src/AspBaseProj.Presentation/Components/Shared/LanguageSelector.razor:1)

```razor
@* LanguageSelector.razor — Dropdown zur Sprachauswahl *@
@inject NavigationManager Navigation
@inject IJSRuntime JS

<div class="dropdown d-inline">
    <button class="btn btn-outline-light btn-sm dropdown-toggle"
            data-bs-toggle="dropdown" aria-expanded="false">
        🌐 @CurrentCultureDisplayName
    </button>
    <ul class="dropdown-menu dropdown-menu-end">
        @foreach (var culture in SupportedCultures)
        {
            <li>
                <a class="dropdown-item @(culture.Name == CurrentCultureName ? "active" : "")"
                   @onclick="() => ChangeCulture(culture.Name)"
                   @onclick:preventDefault
                   href="#">
                    @culture.NativeName
                </a>
            </li>
        }
    </ul>
</div>

@code {
    private static readonly CultureInfo[] SupportedCultures =
    [
        new("en"),
        new("de"),
    ];

    private string CurrentCultureName =>
        CultureInfo.CurrentUICulture.Name;

    private string CurrentCultureDisplayName =>
        CultureInfo.CurrentUICulture.Name switch
        {
            "de" => "Deutsch",
            _ => "English"
        };

    private async Task ChangeCulture(string culture)
    {
        // Setzt das Cookie via JS-Interop (Standard ASP.NET Core Pattern)
        await JS.InvokeVoidAsync("blazorCulture.set", culture);
        Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
    }
}
```

### 4.1 JavaScript-Helper (`wwwroot/js/culture.js`)

```javascript
window.blazorCulture = {
    get: () => document.cookie
        .match(new RegExp('(^| )\\.AspNetCore\\.Culture=([^;]+)'))?.[2]
        ?.split('|')[0]?.replace('c=', ''),
    set: (culture) => {
        document.cookie =
            `.AspNetCore.Culture=c=${culture}|uic=${culture};expires=${new Date(
                Date.now() + 365 * 864e5
            ).toUTCString()};path=/;samesite=strict`;
    }
};
```

### 4.2 Einbindung in `App.razor`

In [`App.razor`](src/AspBaseProj.Presentation/Components/App.razor:1) das Script referenzieren:

```html
<script src="js/culture.js"></script>
```

### 4.3 Einbindung in `MainLayout.razor`

In [`MainLayout.razor`](src/AspBaseProj.Presentation/Components/Layout/MainLayout.razor:1) den `LanguageSelector` in die Navbar einbauen (rechts neben Login/Logout):

```razor
<ul class="navbar-nav">
    <li class="nav-item d-flex align-items-center me-2">
        <LanguageSelector />
    </li>
    @if (IsAuthenticated) { ... } else { ... }
</ul>
```

---

## 5. `_Imports.razor` ergänzen

In [`_Imports.razor`](src/AspBaseProj.Presentation/Components/_Imports.razor:1) folgende Zeilen hinzufügen:

```razor
@using System.Globalization
@using Microsoft.Extensions.Localization
@using AspBaseProj.Presentation.Resources
@inject IStringLocalizer<SharedResource> L
```

Durch das `@inject IStringLocalizer<SharedResource> L` im `_Imports.razor` ist `L` in **allen** Komponenten automatisch verfügbar — ohne dass jede Seite einzeln injizieren muss.

---

## 6. Ressourcen-Schlüssel-Katalog

### 6.1 Namenskonvention

```
{Bereich}_{Element}[_{Zustand}]
```

Beispiele: `Nav_Home`, `Login_Title`, `PostList_NoPosts`, `Common_Loading`, `Common_Cancel`

### 6.2 Vollständiger Schlüssel-Katalog

Nachfolgend alle UI-Strings, die aus dem Code extrahiert wurden, mit ihren Übersetzungsschlüsseln:

#### Common (übergreifend)

| Schlüssel | EN (Default) | DE |
|-----------|-------------|-----|
| `Common_Loading` | Loading... | Laden... |
| `Common_Cancel` | Cancel | Abbrechen |
| `Common_Delete` | Delete | Löschen |
| `Common_Save` | Save | Speichern |
| `Common_View` | View | Ansehen |
| `Common_Open` | Open | Öffnen |
| `Common_SomethingWentWrong` | Something went wrong: {0} | Etwas ist schiefgelaufen: {0} |
| `Common_Yes` | Yes | Ja |
| `Common_No` | No | Nein |
| `Common_Unknown` | Unknown | Unbekannt |
| `Common_Guest` | Guest | Gast |

#### Navigation (MainLayout)

| Schlüssel | EN | DE |
|-----------|----|----|
| `Nav_Brand` | BlazorBlog | BlazorBlog |
| `Nav_Home` | Home | Startseite |
| `Nav_MyPosts` | My Posts | Meine Beiträge |
| `Nav_NewPost` | New Post | Neuer Beitrag |
| `Nav_Moderation` | Moderation | Moderation |
| `Nav_Users` | Users | Benutzer |
| `Nav_Settings` | Settings | Einstellungen |
| `Nav_Logout` | Logout | Abmelden |
| `Nav_Login` | Login | Anmelden |
| `Nav_Register` | Register | Registrieren |
| `Nav_Profile` | Profile | Profil |
| `Nav_ApiDocs` | API Docs | API-Doku |
| `Nav_SignOut` | Sign out | Abmelden |
| `Footer_Description` | A multi-user blog platform built with .NET 10, ASP.NET Core & BootstrapBlazor. | Eine Mehrbenutzer-Blog-Plattform mit .NET 10, ASP.NET Core & BootstrapBlazor. |
| `Footer_Navigation` | Navigation | Navigation |
| `Footer_Account` | Account | Konto |
| `Footer_Copyright` | © 2026 BlazorBlog — Powered by ASP.NET Core & BootstrapBlazor | © 2026 BlazorBlog — Powered by ASP.NET Core & BootstrapBlazor |

#### Home / Post List

| Schlüssel | EN | DE |
|-----------|----|----|
| `Home_Title` | Latest Posts | Neueste Beiträge |
| `Home_NoPosts` | No posts yet. | Noch keine Beiträge. |
| `Home_WriteFirstPost` | Write the first post | Ersten Beitrag schreiben |
| `PostList_By` | By | Von |
| `PostList_LikeCount` | 👍 {0} | 👍 {0} |
| `PostList_DislikeCount` | 👎 {0} | 👎 {0} |

#### Post Detail

| Schlüssel | EN | DE |
|-----------|----|----|
| `PostDetail_Draft` | 📝 Draft | 📝 Entwurf |
| `PostDetail_Created` | Created | Erstellt |
| `PostDetail_PublishNow` | ✅ Publish Now | ✅ Jetzt veröffentlichen |
| `PostDetail_DeleteDraft` | 🗑️ Delete Draft | 🗑️ Entwurf löschen |
| `PostDetail_Edit` | ✏️ Edit | ✏️ Bearbeiten |
| `PostDetail_DeleteTitle` | Delete Draft | Entwurf löschen |
| `PostDetail_DeleteMessage` | Are you sure you want to delete "{0}"? | Möchten Sie "{0}" wirklich löschen? |
| `PostDetail_DeleteWarning` | This action cannot be undone. | Diese Aktion kann nicht rückgängig gemacht werden. |
| `PostDetail_Publishing` | Publishing... | Wird veröffentlicht... |
| `PostDetail_Published` | Published! Refreshing... | Veröffentlicht! Aktualisierung... |
| `PostDetail_FailedPublish` | Failed to publish. | Veröffentlichung fehlgeschlagen. |
| `PostDetail_FailedDelete` | Failed to delete. | Löschen fehlgeschlagen. |
| `PostDetail_PostNotFound` | Post not found (ID: {0}). | Beitrag nicht gefunden (ID: {0}). |
| `PostDetail_LoginToRate` | Login to rate | Anmelden zum Bewerten |
| `PostDetail_Bookmark` | Bookmark | Lesezeichen |
| `PostDetail_Bookmarked` | Bookmarked | Lesezeichen gesetzt |

#### Comments

| Schlüssel | EN | DE |
|-----------|----|----|
| `Comments_Title` | 💬 Comments | 💬 Kommentare |
| `Comments_AddTitle` | Add a Comment | Kommentar hinzufügen |
| `Comments_PlaceholderName` | Your Name | Ihr Name |
| `Comments_PlaceholderEmail` | Your Email | Ihre E-Mail |
| `Comments_PlaceholderText` | Write a comment... | Kommentar schreiben... |
| `Comments_Submit` | Submit | Absenden |
| `Comments_Posting` | Posting... | Wird gesendet... |
| `Comments_NoComments` | No comments yet. Be the first to comment! | Noch keine Kommentare. Seien Sie der Erste! |
| `Comments_PendingApproval` | ⏳ Pending Approval | ⏳ Wartet auf Freigabe |
| `Comments_ThankYouTitle` | ✅ Thank you! | ✅ Danke! |
| `Comments_ThankYouBody` | Your comment has been submitted and is pending approval by an administrator. | Ihr Kommentar wurde gesendet und wartet auf Freigabe durch einen Administrator. |
| `Comments_WriteAnother` | Write another comment | Weiteren Kommentar schreiben |
| `Comments_FailedPost` | Failed to post comment. Please try again. | Kommentar konnte nicht gesendet werden. Bitte erneut versuchen. |
| `Comments_FailedLoad` | Failed to load comments. | Kommentare konnten nicht geladen werden. |
| `Comments_Reply` | 💬 Reply | 💬 Antworten |
| `Comments_ReplyPlaceholder` | Write a reply... | Antwort schreiben... |
| `Comments_SubmitReply` | Submit Reply | Antwort senden |
| `Comments_RepliesClosed` | Replies are closed for this thread | Antworten sind in diesem Thread geschlossen |
| `Comments_Anonymous` | Anonymous | Anonym |
| `Comments_FailedReply` | Failed to post reply. Please try again. | Antwort konnte nicht gesendet werden. Bitte erneut versuchen. |

#### Login

| Schlüssel | EN | DE |
|-----------|----|----|
| `Login_Title` | Login | Anmelden |
| `Login_Username` | Username | Benutzername |
| `Login_Password` | Password | Passwort |
| `Login_Button` | Login | Anmelden |
| `Login_LoggingIn` | Logging in... | Anmeldung... |
| `Login_OrLoginWith` | Or login with | Oder anmelden mit |
| `Login_NoAccount` | Don't have an account? | Noch kein Konto? |
| `Login_RegisterLink` | Register | Registrieren |
| `Login_InvalidCredentials` | Invalid credentials. | Ungültige Anmeldedaten. |

#### Register

| Schlüssel | EN | DE |
|-----------|----|----|
| `Register_Title` | Register | Registrieren |
| `Register_Username` | Username | Benutzername |
| `Register_EmailOptional` | Email (optional) | E-Mail (optional) |
| `Register_Password` | Password | Passwort |
| `Register_Button` | Register | Registrieren |
| `Register_Registering` | Registering... | Registrierung... |
| `Register_OrSignUpWith` | Or sign up with | Oder registrieren mit |
| `Register_HaveAccount` | Already have an account? | Bereits ein Konto? |
| `Register_LoginLink` | Login | Anmelden |
| `Register_Failed` | Registration failed. The username may already be taken. | Registrierung fehlgeschlagen. Der Benutzername ist möglicherweise vergeben. |

#### Post Editor

| Schlüssel | EN | DE |
|-----------|----|----|
| `Editor_NewPost` | ✏️ New Post | ✏️ Neuer Beitrag |
| `Editor_EditPost` | ✏️ Edit Post | ✏️ Beitrag bearbeiten |
| `Editor_Title` | Title | Titel |
| `Editor_TitlePlaceholder` | Enter post title... | Beitragstitel eingeben... |
| `Editor_Content` | Content (Markdown) | Inhalt (Markdown) |
| `Editor_ContentPlaceholder` | Write your post in Markdown... | Beitrag in Markdown schreiben... |
| `Editor_Published` | Published | Veröffentlicht |
| `Editor_Create` | Create | Erstellen |
| `Editor_Update` | Update | Aktualisieren |
| `Editor_PostNotFound` | Post not found. | Beitrag nicht gefunden. |
| `Editor_FailedCreate` | Failed to create post. | Beitrag konnte nicht erstellt werden. |
| `Editor_FailedUpdate` | Failed to update post. | Beitrag konnte nicht aktualisiert werden. |
| `Editor_FailedDelete` | Failed to delete post. | Beitrag konnte nicht gelöscht werden. |

#### Image Uploader

| Schlüssel | EN | DE |
|-----------|----|----|
| `Images_Title` | 🖼️ Images | 🖼️ Bilder |
| `Images_Upload` | Upload Image | Bild hochladen |
| `Images_Uploading` | Uploading... | Wird hochgeladen... |
| `Images_UploadButton` | Upload | Hochladen |
| `Images_Loading` | Loading images... | Bilder werden geladen... |
| `Images_None` | No images uploaded yet. | Noch keine Bilder hochgeladen. |
| `Images_CopyMarkdown` | 📋 Copy Markdown | 📋 Markdown kopieren |
| `Images_FailedUpload` | Failed to upload image. | Bild konnte nicht hochgeladen werden. |

#### Social Embed

| Schlüssel | EN | DE |
|-----------|----|----|
| `Social_Open` | Open | Öffnen |
| `Social_YouTube` | YouTube Video | YouTube-Video |
| `Social_Vimeo` | Vimeo Video | Vimeo-Video |
| `Social_Twitter` | X / Twitter Post | X / Twitter-Beitrag |
| `Social_ExternalLink` | External Link | Externer Link |

#### My Posts (Author Dashboard)

| Schlüssel | EN | DE |
|-----------|----|----|
| `MyPosts_Title` | 📋 My Posts | 📋 Meine Beiträge |
| `MyPosts_All` | All | Alle |
| `MyPosts_Published` | ✅ Published | ✅ Veröffentlicht |
| `MyPosts_Drafts` | 📝 Drafts | 📝 Entwürfe |
| `MyPosts_Bookmarks` | 🔖 Bookmarks | 🔖 Lesezeichen |
| `MyPosts_NewPost` | ✏️ New Post | ✏️ Neuer Beitrag |
| `MyPosts_NoDrafts` | No drafts. | Keine Entwürfe. |
| `MyPosts_NoPublished` | No published posts. | Keine veröffentlichten Beiträge. |
| `MyPosts_NoPosts` | No posts yet. | Noch keine Beiträge. |
| `MyPosts_NoBookmarks` | No bookmarked posts. | Keine Lesezeichen. |
| `MyPosts_ColTitle` | Title | Titel |
| `MyPosts_ColAuthor` | Author | Autor |
| `MyPosts_ColStatus` | Status | Status |
| `MyPosts_ColComments` | 💬 Comments | 💬 Kommentare |
| `MyPosts_ColCreated` | Created | Erstellt |
| `MyPosts_ColActions` | Actions | Aktionen |
| `MyPosts_BadgePublished` | Published | Veröffentlicht |
| `MyPosts_BadgeDraft` | Draft | Entwurf |
| `MyPosts_DeleteTitle` | Delete Post | Beitrag löschen |
| `MyPosts_DeleteMessage` | Are you sure you want to delete "{0}"? | Möchten Sie "{0}" wirklich löschen? |
| `MyPosts_PublishedSuccess` | Post published successfully! | Beitrag erfolgreich veröffentlicht! |
| `MyPosts_DeletedSuccess` | Post deleted. | Beitrag gelöscht. |
| `MyPosts_FailedLoad` | Failed to load posts. | Beiträge konnten nicht geladen werden. |
| `MyPosts_FailedLoadBookmarks` | Failed to load bookmarks. | Lesezeichen konnten nicht geladen werden. |
| `MyPosts_FailedPublish` | Failed to publish post. | Beitrag konnte nicht veröffentlicht werden. |
| `MyPosts_FailedDelete` | Failed to delete post. | Beitrag konnte nicht gelöscht werden. |
| `MyPosts_PostNotFound` | Post not found. | Beitrag nicht gefunden. |
| `MyPosts_TooltipEdit` | Edit | Bearbeiten |
| `MyPosts_TooltipPublish` | Publish | Veröffentlichen |
| `MyPosts_TooltipDelete` | Delete | Löschen |

#### Moderation Queue

| Schlüssel | EN | DE |
|-----------|----|----|
| `Mod_Title` | ✅ Moderation Queue | ✅ Moderations-Warteschlange |
| `Mod_NoPending` | No pending comments. | Keine ausstehenden Kommentare. |
| `Mod_AllReviewed` | All comments have been reviewed. | Alle Kommentare wurden überprüft. |
| `Mod_ApproveSelected` | ✅ Approve Selected ({0}) | ✅ Ausgewählte freigeben ({0}) |
| `Mod_RejectSelected` | ❌ Reject Selected ({0}) | ❌ Ausgewählte ablehnen ({0}) |
| `Mod_ColAuthor` | Author | Autor |
| `Mod_ColContent` | Content | Inhalt |
| `Mod_ColDate` | Date | Datum |
| `Mod_ColActions` | Actions | Aktionen |
| `Mod_Approve` | Approve | Freigeben |
| `Mod_Reject` | Reject | Ablehnen |
| `Mod_Approved` | Comment approved. | Kommentar freigegeben. |
| `Mod_Rejected` | Comment rejected. | Kommentar abgelehnt. |
| `Mod_BulkApproved` | Approved {0} comment(s). | {0} Kommentar(en) freigegeben. |
| `Mod_BulkRejected` | Rejected {0} comment(s). | {0} Kommentar(en) abgelehnt. |

#### User Management

| Schlüssel | EN | DE |
|-----------|----|----|
| `Users_Title` | 👥 User Management | 👥 Benutzerverwaltung |
| `Users_BulkActions` | Bulk Actions: | Massenaktionen: |
| `Users_Selected` | {0} user(s) selected | {0} Benutzer ausgewählt |
| `Users_AssignGroup` | + Assign Group | + Gruppe zuweisen |
| `Users_RemoveGroup` | - Remove Group | - Gruppe entfernen |
| `Users_DeleteSelected` | 🗑️ Delete Selected | 🗑️ Ausgewählte löschen |
| `Users_ColUsername` | Username | Benutzername |
| `Users_ColEmail` | Email | E-Mail |
| `Users_ColGroups` | Groups | Gruppen |
| `Users_ColRoot` | Root | Root |
| `Users_AddGroup` | + Group | + Gruppe |
| `Users_RootCannotModify` | Root — cannot modify | Root — kann nicht geändert werden |
| `Users_GroupAssigned` | Group assigned. | Gruppe zugewiesen. |
| `Users_GroupRemoved` | Group removed. | Gruppe entfernt. |
| `Users_UserDeleted` | User deleted. | Benutzer gelöscht. |
| `Users_BulkGroupAssigned` | Group assigned to selected users. | Gruppe ausgewählten Benutzern zugewiesen. |
| `Users_BulkGroupRemoved` | Group removed from selected users. | Gruppe von ausgewählten Benutzern entfernt. |
| `Users_BulkDeleted` | Selected users deleted. | Ausgewählte Benutzer gelöscht. |

#### Settings

| Schlüssel | EN | DE |
|-----------|----|----|
| `Settings_Title` | ⚙️ System Settings | ⚙️ Systemeinstellungen |
| `Settings_Save` | 💾 Save | 💾 Speichern |
| `Settings_Saved` | Setting "{0}" saved. | Einstellung "{0}" gespeichert. |

#### Profile

| Schlüssel | EN | DE |
|-----------|----|----|
| `Profile_Title` | 👤 Profile | 👤 Profil |
| `Profile_Username` | Username | Benutzername |
| `Profile_Email` | Email | E-Mail |
| `Profile_EmailNotSet` | Not set | Nicht gesetzt |
| `Profile_Groups` | Groups | Gruppen |
| `Profile_NoGroups` | None (Root has unrestricted access) | Keine (Root hat uneingeschränkten Zugriff) |
| `Profile_RootBadge` | Root | Root |

#### Delete Confirm Modal

| Schlüssel | EN | DE |
|-----------|----|----|
| `DeleteModal_Delete` | Delete | Löschen |
| `DeleteModal_Deleting` | Deleting... | Wird gelöscht... |
| `DeleteModal_Warning` | This action cannot be undone. | Diese Aktion kann nicht rückgängig gemacht werden. |

---

## 7. Umstellung der Komponenten — Muster

### 7.1 Vorher (hardcoded)

```razor
<h2>Login</h2>
<label class="form-label">Username</label>
<button>@(isLoading ? "Logging in..." : "Login")</button>
```

### 7.2 Nachher (lokalisiert)

```razor
<h2>@L["Login_Title"]</h2>
<label class="form-label">@L["Login_Username"]</label>
<button>@(isLoading ? L["Login_LoggingIn"] : L["Login_Button"])</button>
```

### 7.3 Strings mit Parametern

Für Strings mit Platzhaltern (z.B. "Approved {0} comment(s)."):

```razor
@L["Mod_BulkApproved", selectedIds.Count]
```

In der `.resx`-Datei steht: `Approved {0} comment(s).` / `{0} Kommentar(en) freigegeben.`

### 7.4 `@code`-Block Strings

Strings in `@code`-Blöcken (z.B. `error = "Post not found."`) werden über `L["..."]` übersetzt. Da `L` im `_Imports.razor` global injiziert wird, ist es auch im `@code`-Block verfügbar:

```razor
@code {
    // ...
    error = L["PostDetail_PostNotFound"];
    successMessage = L["MyPosts_PublishedSuccess"];
}
```

---

## 8. Implementierungs-Schritte (Reihenfolge)

### Schritt 1: Infrastruktur einrichten
1. `Resources/`-Ordner erstellen
2. `SharedResource.cs` (leere Marker-Klasse) anlegen
3. `SharedResource.resx` (Neutral/EN-Default) anlegen
4. `SharedResource.de.resx` (Deutsch) anlegen
5. `wwwroot/js/culture.js` anlegen
6. `Program.cs` — Localization-Services + Middleware registrieren
7. `App.razor` — `<script src="js/culture.js">` einbinden
8. `_Imports.razor` — `@using` + `@inject IStringLocalizer<SharedResource> L`

### Schritt 2: LanguageSelector-Komponente
1. `Components/Shared/LanguageSelector.razor` erstellen
2. In `MainLayout.razor` in die Navbar einbauen

### Schritt 3: Ressourcen befüllen
1. Alle Schlüssel aus dem Katalog (Abschnitt 6) in `SharedResource.resx` (EN) eintragen
2. Alle Schlüssel in `SharedResource.de.resx` (DE) mit deutschen Übersetzungen eintragen

### Schritt 4: Komponenten umstellen (iterativ)
Pro Komponente alle hardcoded Strings durch `@L["..."]` ersetzen. Empfohlene Reihenfolge:
1. `MainLayout.razor` (Navigation — höchste Sichtbarkeit)
2. `Home.razor` + `PostList.razor`
3. `Login.razor` + `Register.razor`
4. `PostDetail.razor` + `RatingBar.razor` + `BookmarkButton.razor`
5. `CommentSection.razor` + `CommentNode.razor`
6. `PostEditor.razor` + `MarkdownEditor.razor` + `ImageUploader.razor` + `SocialEmbed.razor`
7. `MyPosts.razor`
8. `Moderation.razor`
9. `UserManagement.razor`
10. `Settings.razor` + `Profile.razor`
11. `AlertMessage.razor` + `DeleteConfirmModal.razor` + `Pager.razor`

### Schritt 5: Testen
1. App starten, Sprache über Dropdown wechseln
2. Alle Seiten in beiden Sprachen durchklicken
3. Cookie-Persistenz testen (Browser schließen, neu öffnen)
4. Prüfen, dass keine hardcoded Strings übrig sind

### Schritt 6: UI-Specification aktualisieren
Gemäß Projekt-Workflow: [`.roo/rules/ui-specification.md`](.roo/rules/ui-specification.md:1) um den `LanguageSelector` ergänzen.

---

## 9. Weitere Sprachen hinzufügen (zukünftig)

Um z.B. Französisch hinzuzufügen:

1. `SharedResource.fr.resx` anlegen (alle Schlüssel mit französischen Übersetzungen)
2. In `Program.cs` `new CultureInfo("fr")` zum `supportedCultures`-Array hinzufügen
3. In `LanguageSelector.razor` `new("fr")` zum `SupportedCultures`-Array hinzufügen

**Keine weiteren Code-Änderungen** in den Komponenten nötig — die `IStringLocalizer`-Infrastruktur wählt automatisch die richtige `.resx`-Datei basierend auf der aktiven Kultur.

---

## 10. Technische Notizen

### 10.1 Warum `forceLoad: true` beim Kulturwechsel?

Blazor Interactive Server hält eine SignalR-Verbindung. Die Kultur wird beim **Aufbau des Circuits** gelesen. Ein einfaches `StateHasChanged()` reicht nicht — der Circuit muss neu aufgebaut werden. `Navigation.NavigateTo(uri, forceLoad: true)` erzwingt einen Full-Page-Reload, der den Circuit neu initialisiert mit der neuen Kultur aus dem Cookie.

Dies ist der von Microsoft dokumentierte Ansatz für Blazor Server Localization.

### 10.2 `AsNoTracking` und Kultur

Die Kultur-Einstellung hat keinen Einfluss auf die Datenbank- oder Application-Schicht. Sie betrifft **ausschließlich** die Darstellung von UI-Strings. Datumsformate (`ToString("d")`) werden automatisch kulturabhängig formatiert, da `CultureInfo.CurrentCulture` gesetzt wird.

### 10.3 Keine Änderung an Domain/Application/Infrastructure

Die Lokalisierung betrifft **nur** die Presentation-Schicht. Keine Änderungen an Entities, DTOs, Repositories oder Services. Dies entspricht der Clean-Architecture-Regel: UI-spezifische Logik bleibt in der äußersten Schicht.
