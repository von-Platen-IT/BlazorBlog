# Einfacher Blog – Funktionsbeschreibung (Soll-Konzept)

Bei diesem Projekt handelt es sich um eine ASP.NET Core Webanwendung auf Basis von .NET 10.  
Die Anwendung bietet eine mehrbenutzerfähige Blog-Plattform mit rollenbasierter Zugriffssteuerung, einem komfortablen MD-Editor für Autoren, verschachtelten Kommentaren und einem responsiven Design.  
Die Datenhaltung erfolgt über eine PostgreSQL-Datenbank, die in einem Docker-Container betrieben wird.

## Features (Soll-Beschreibung)

### Benutzerverwaltung und Rollen
- Registrierung und Anmeldung von Benutzern.
- Vordefinierte Benutzergruppen: **Author**, **Admin**, **Viewer**, **Public**.
- Der Benutzer **root** besitzt uneingeschränkte Rechte: Er kann sämtliche Inhalte (Beiträge, Kommentare) bearbeiten und löschen, die Gruppenzugehörigkeit anderer Benutzer verwalten sowie sämtliche administrativen Aufgaben ausführen. Er ist keiner Gruppe zugeordnet.

### Blog-Beiträge (Posts)
- Angemeldete Benutzer der Gruppe **Author** können neue Blog-Beiträge verfassen und editieren sowie formattieren.
- Autoren dürfen ihre eigenen Beiträge nachträglich bearbeiten und löschen.
- Beiträge werden in einer Übersicht dargestellt (z. B. chronologisch) und können von allen Besuchern gelesen werden.
- Das Hochladen von Grafiken in die Dokumente ist möglich. Diese werden in der DB gespeichert
- Berücksichtigung finden andere soziale Medien und Videoplatformen auf die in den Beiträgen optisch ansprechend verlinkt werden kann.

### Kommentarsystem
- Angemeldete Benutzer (aller Gruppen) können Beiträge direkt kommentieren.
- Kommentare lassen sich wiederum kommentieren (verschachtelte Diskussionen).
- Nicht angemeldete Besucher können ebenfalls Kommentare verfassen; diese werden jedoch erst nach manueller Freischaltung durch einen Administrator oder root öffentlich sichtbar.

### Moderation und Freischaltung
- Administratoren und root erhalten eine Übersicht über alle noch nicht freigegebenen Gästekommentare und können diese einzeln oder gesammelt freischalten oder verwerfen.
- Gemeldete oder unangemessene Inhalte können von Administratoren/root entfernt werden.

### Responsives Design
- Die gesamte Benutzeroberfläche passt sich automatisch an verschiedene Geräteklassen an (Desktop-Browser, Tablet, Smartphone), sodass alle Funktionen auf jedem Endgerät gleichermaßen bedienbar sind. Es wird eine SPA entwickelt die ohne round-trips auskommt.

### Administrationsbereich 
- Alle Benter in der Gruppe **Admin** haben die Möglichkeit zu Administrieren.
- Sie haben nur die Rechte bezogen auf Beiträge. Das System wird ausschließlich von **root** verwaltet.
- Verwaltung von Benutzern und deren Gruppenzugehörigkeit.
- Vollständige Kontrolle über sämtliche Inhalte: Beiträge und Kommentare beliebiger Autoren editieren oder löschen.
- Option zur Konfiguration grundlegender Systemeinstellungen (z. B. Blog-Titel, Freigabeprozesse).

### Web API
- Funktion des UI die in den Gruppen Author und Viewer über den Browser angeboten werden stehen auch über eine API zur Verfügung
- Für die Verwendung der API werden zeitgemäße Sicherheitsmechanismen verwendet.
- die API Schnittstelle wird für den Einsatz von Swagger vorbereitet.

---

## Feature-Liste für die Konfiguration eines AI-Agenten

1. **Mehrbenutzer-Blog mit Rollen**  
   - Gruppen: Author, Admin, Viewer.  
   - root-Konto mit Superuser-Rechten.

2. **Beitragserstellung und -bearbeitung**  
   - Rich-Text-Editor mit grundsätzlichen Textlayout Funktionen für Autoren.  
   - CRUD-Operationen nur für eigene Beiträge (Author) bzw. alles (Admin/root).

3. **Kommentarsystem**  
   - Authentifizierte Benutzer: sofort sichtbare Kommentare.  
   - Gäste: moderierte Kommentare (Freischaltung erforderlich).  
   - Verschachtelte Kommentare (Antworten auf Kommentare).

4. **Moderations-Workflow**  
   - Liste ausstehender Gästekommentare.  
   - Massen- und Einzelfreischaltung durch Admin/root.

5. **Responsive Oberfläche**  
   - Optimiert für Desktop, Tablet und Mobilgeräte.

6. **Datenbank-Backend**  
   - PostgreSQL, bereitgestellt in einem Docker-Container auf dem lokalen host.
   - initial config: 
   sudo docker run --name aspbaseporj_db \
  -e POSTGRES_USER=admin \
  -e POSTGRES_PASSWORD=gandalf123! \
  -e POSTGRES_DB=deine_datenbank \
  --network host \
  -v postgres_abp_data:/var/lib/postgresql/data \
  --restart unless-stopped \
  -d postgres:17


7. **Benutzer- und Rechteverwaltung**  
   - root kann Gruppen ändern und alle Inhalte global bearbeiten/löschen.

---

## To Run

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for the PostgreSQL container)
- A free TCP port `5433` on the host (used for PostgreSQL)

### 1. Start PostgreSQL

The database runs in a Docker container using the `postgres:17` image. The
command below uses line continuations (`\`) so each option is readable and
reviewable. The named volume `postgres_abp_data` persists data across container
restarts, and `--restart unless-stopped` ensures the container survives host
reboots.

**Note:** The container maps host port `5433` to container port `5432` to avoid
conflicts if port `5432` is already in use on your host.

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

> **Security note:** The credentials above are development defaults that also
> appear in [`appsettings.json`](src/AspBaseProj.Presentation/appsettings.json:3).
> For any non-local deployment, override them via environment variables or
> [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
> (e.g. `ConnectionStrings__DefaultConnection`) instead of editing the file.

Verify the container is healthy before launching the app:

```bash
docker ps --filter name=aspbaseporj_db
docker logs aspbaseporj_db
```

If the container already exists from a previous run, start it with
`docker start aspbaseporj_db` instead of `docker run`.

### 2. Run the app

```bash
dotnet run --project src/AspBaseProj.Presentation
```

The application listens on the default ASP.NET Core URLs (usually
`http://localhost:5000`). Use `--urls` to override, e.g.
`dotnet run --project src/AspBaseProj.Presentation --urls http://localhost:8080`.

### 3. First launch

On first launch the application **auto-migrates** the database and **seeds** the
root superuser account. The seeded credentials are configured in
[`appsettings.json`](src/AspBaseProj.Presentation/appsettings.json:11) under the
`Blog` section:

| Field        | Value          |
|--------------|----------------|
| Username     | `root`         |
| Password     | `Root#12345!`  |

Change the root password immediately after the first login.
