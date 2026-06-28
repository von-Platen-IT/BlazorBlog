# BlazorBlog — Funktionsbeschreibung (Soll-Konzept)

Bei diesem Projekt handelt es sich um eine ASP.NET Core Webanwendung auf Basis von .NET 10 mit **Blazor Interactive Server**.
Die Anwendung bietet eine mehrbenutzerfähige Blog-Plattform mit rollenbasierter Zugriffssteuerung, einem komfortablen Markdown-Editor für Autoren, verschachtelten Kommentaren mit visuellen Tree-Lines und einem responsiven Design.
Die Datenhaltung erfolgt über eine PostgreSQL-Datenbank, die in einem Docker-Container betrieben wird.

**Architektur-Philosophie:** Die Applikation ist als **SPA (Single Page Application)** konzipiert, die sich wie eine **Desktop-Anwendung anfühlt** — auch im Browser. Sämtliche Interaktionen (Kommentare, Bewertungen, Bookmarks, Navigation) erfolgen **ohne Full-Page-Roundtrips** über Blazor Interactive Server mit SignalR. Es wird durchgängig modernste Razor-Technologie verwendet (`@rendermode InteractiveServer`, `StreamRendering`, `@onclick` mit `async`/`await`).

---

## Features

### Benutzerverwaltung und Rollen
- Registrierung und Anmeldung von Benutzern (lokal + OAuth: Google, GitHub, Microsoft).
- Vordefinierte Benutzergruppen: **Author**, **Admin**, **Viewer**, **Public**.
- Der Benutzer **root** besitzt uneingeschränkte Rechte: Er kann sämtliche Inhalte (Beiträge, Kommentare) bearbeiten und löschen, die Gruppenzugehörigkeit anderer Benutzer verwalten sowie sämtliche administrativen Aufgaben ausführen. Er ist keiner Gruppe zugeordnet.

### Blog-Beiträge (Posts)
- Angemeldete Benutzer der Gruppe **Author** können neue Blog-Beiträge verfassen, als Draft speichern und später veröffentlichen.
- Autoren haben eine **"My Posts"-Übersicht** (`/my/posts`) mit allen eigenen Beiträgen (Drafts + Published), Kommentar-Zählern und Quick-Publish-Funktion.
- Autoren dürfen ihre eigenen Beiträge nachträglich bearbeiten und löschen.
- Beiträge werden in einer Übersicht chronologisch dargestellt und können von allen Besuchern gelesen werden.
- Das Hochladen von Bildern ist möglich; diese werden als `byte[]` in der Datenbank gespeichert.
- Soziale Medien und Videoplattformen können optisch ansprechend in Beiträge eingebunden werden.

### Bewertungssystem (Like/Dislike)
- Authentifizierte Benutzer können Beiträge mit 👍 (Like) oder 👎 (Dislike) bewerten.
- **Toggle-Logik:** Erneutes Klicken entfernt die Bewertung; Like und Dislike schließen sich gegenseitig aus.
- **Ein Vote pro Benutzer pro Beitrag** (Unique-Constraint auf `PostId + UserId`).
- Alle Besucher sehen die Gesamtanzahl der Likes und Dislikes auf der Home-Seite und in der Post-Detail-Ansicht.

### Bookmark-System
- Authentifizierte Benutzer können Beiträge mit einem 🔖-Button bookmarken (Toggle).
- **Ein Bookmark pro Benutzer pro Beitrag** (Unique-Constraint auf `PostId + UserId`).
- Gespeicherte Beiträge sind im **"My Posts"-Bereich** unter dem **"🔖 Bookmarks"-Tab** abrufbar — mit Autor, Like/Dislike/Comment-Zählern.

### Kommentarsystem (Modul mit Tree-Lines)
- **Eigenständiges Modul** (`CommentSection` + `CommentNode`), gekapselt und ohne Event-Bubbling.
- Angemeldete Benutzer können Beiträge direkt kommentieren — der Kommentar **erscheint sofort** (optimistic insert, kein Reload).
- Kommentare lassen sich wiederum kommentieren (verschachtelte Diskussionen) — Replies **erscheinen sofort**.
- **Visuelle Hierarchie:** Neben der Einrückung werden **graue Verbindungslinien** (Tree-Lines) zwischen Parent und Reply angezeigt.
- Nicht angemeldete Besucher können ebenfalls kommentieren; ihr Kommentar erscheint sofort mit einem ⏳ **"Pending Approval"**-Badge und wird nach Freischaltung öffentlich sichtbar.
- **Kein Round-Trip:** Kommentar-Operationen nutzen die API-Antwort direkt für optimistische UI-Updates — kein vollständiger Neu-Load des Kommentar-Baums.

### Moderation und Freischaltung
- Administratoren und root erhalten eine Übersicht über alle noch nicht freigegebenen Gästekommentare und können diese einzeln oder gesammelt freischalten oder verwerfen.

### Responsives Design
- Die gesamte Benutzeroberfläche passt sich automatisch an Desktop, Tablet und Smartphone an.
- Alle Funktionen sind auf jedem Gerät gleichermaßen bedienbar.
- Die SPA verwendet client-seitiges Routing ohne Full-Page-Reloads.

### Administrationsbereich 
- Benutzer in der Gruppe **Admin** können Beiträge und Kommentare moderieren.
- Das System wird ausschließlich von **root** verwaltet (Benutzer, Gruppen, Systemeinstellungen).
- Vollständige Kontrolle über sämtliche Inhalte: Beiträge und Kommentare beliebiger Autoren editieren oder löschen.
- Option zur Konfiguration grundlegender Systemeinstellungen (Blog-Titel, Freigabeprozesse).

### Web API
- Alle UI-Funktionen stehen auch über eine REST-API zur Verfügung.
- Zeitgemäße Sicherheitsmechanismen (JWT + Cookie-Authentication).
- Swagger/OpenAPI-Dokumentation unter `/swagger`.

---

## Feature-Liste für die Konfiguration eines AI-Agenten

1. **Mehrbenutzer-Blog mit Rollen**  
   - Gruppen: Author, Admin, Viewer, Public.  
   - root-Konto mit Superuser-Rechten.

2. **Beitragserstellung und -bearbeitung**  
   - Markdown-Editor für Autoren.  
   - CRUD-Operationen nur für eigene Beiträge (Author) bzw. alles (Admin/root).
   - Draft/Publish-Workflow mit "My Posts"-Dashboard.

3. **Kommentarsystem (Modul)**  
   - Authentifizierte Benutzer: sofort sichtbare Kommentare (optimistic insert).  
   - Gäste: moderierte Kommentare mit sofortigem "Pending Approval"-Badge.  
   - Verschachtelte Kommentare mit visuellen Tree-Lines.
   - Kein Round-Trip: API-Antwort wird direkt in den lokalen State eingefügt.

4. **Bewertungs- & Bookmark-System**
   - Like/Dislike mit Toggle-Logik und Unique-Constraint.
   - Bookmarks mit Toggle und "Bookmarks"-Tab in My Posts.

5. **Moderations-Workflow**  
   - Liste ausstehender Gästekommentare.  
   - Massen- und Einzelfreischaltung durch Admin/root.

6. **Responsive Oberfläche**
   - Optimiert für Desktop, Tablet und Mobilgeräte.
   - SPA ohne Full-Page-Roundtrips (Blazor Interactive Server + SignalR).

7. **Mehrsprachigkeit (i18n)**
   - Das UI unterstützt mehrere Sprachen — aktuell **Englisch** (Standard) und **Deutsch**.
   - Ein **Sprach-Dropdown** (🌐) in der Navigationsleiste erlaubt das Umschalten zur Laufzeit.
   - Die gewählte Sprache wird in einem Cookie gespeichert und beim nächsten Besuch wiederhergestellt.
   - Technisch basiert die Lokalisierung auf der nativen ASP.NET Core `IStringLocalizer`-Infrastruktur mit `.resx`-Ressourcendateien.

9. **Datenbank-Backend**
   - PostgreSQL, bereitgestellt in einem Docker-Container.
   - Auto-Migration beim ersten Start.

10. **Benutzer- und Rechteverwaltung**
   - root kann Gruppen ändern und alle Inhalte global bearbeiten/löschen.

---

## To Run

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for the PostgreSQL container)
- A free TCP port `5433` on the host (used for PostgreSQL)

### 1. Start PostgreSQL

```bash
docker run \
  --name aspbaseporj_db \
  -e POSTGRES_USER=admin \
  -e POSTGRES_PASSWORD=gandalf123! \
  -e POSTGRES_DB=blasor_blog_db \
  -p 5433:5432 \
  -v blasor_blog_data:/var/lib/postgresql/data \
  --restart unless-stopped \
  -d postgres:17
```

> **Security note:** The credentials above are development defaults. For any non-local deployment, override them via environment variables or user secrets.

### 2. Run the app

```bash
dotnet run --project src/AspBaseProj.Presentation
```

The application listens on `http://localhost:5113`.

### 3. First launch

On first launch the application **auto-migrates** the database and **seeds** the root superuser account:

| Field        | Value          |
|--------------|----------------|
| Username     | `root`         |
| Password     | `Root#12345!`  |

Change the root password immediately after the first login.

---

## Adding New Languages

The UI uses ASP.NET Core's native `IStringLocalizer` infrastructure with `.resx` resource files. Adding a new language requires **three steps** — no component code changes needed.

### Step 1: Create a new `.resx` resource file

Copy the existing `SharedResource.resx` (English) and rename it with the new culture code:

```
src/AspBaseProj.Presentation/Resources/SharedResource.{culture}.resx
```

For example, to add French:

```
src/AspBaseProj.Presentation/Resources/SharedResource.fr.resx
```

Translate all `<value>` entries to the new language. The `<data name="...">` keys must remain identical.

### Step 2: Register the culture in `Program.cs`

Add the new `CultureInfo` to the `supportedCultures` array in [`Program.cs`](src/AspBaseProj.Presentation/Program.cs:34):

```csharp
var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("de"),
    new CultureInfo("fr"),   // <-- new language
};
```

### Step 3: Add the language to the `LanguageSelector` dropdown

Add the new culture to the `SupportedCultures` array in [`LanguageSelector.razor`](src/AspBaseProj.Presentation/Components/Shared/LanguageSelector.razor:24):

```csharp
private static readonly CultureInfo[] SupportedCultures =
[
    new("en"),
    new("de"),
    new("fr"),   // <-- new language
];
```

That's it — rebuild and the new language appears in the 🌐 dropdown. The `IStringLocalizer` automatically picks the correct `.resx` file based on the active culture.
