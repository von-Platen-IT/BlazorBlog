# .NET 10 SPA Fullstack Development Rules (ASP.NET Core, PostgreSQL, EF Core, BlazorStrap)

## 1. Clean Architecture erzwingen
Strikte Trennung in Domain, Application, Infrastructure und Presentation. Abhängigkeiten zeigen *immer* nach innen (zur Domain).

## 2. Dependency Inversion
Abstrahiere alle externen Abhängigkeiten (Datenbank, externe APIs) hinter Interfaces. Implementierungsdetails gehören in die Infrastructure-Layer.

## 3. Single Responsibility
Jede Klasse und Methode hat genau einen Grund, sich zu ändern.

## 4. Durchgängiges async/await
Alle I/O-Operationen (Datenbank, HTTP, Dateisystem) müssen asynchron sein.

## 5. Kein Blocking
Die Verwendung von `.Result`, `.Wait()` oder `Task.Run` für I/O-Bound-Operations ist strikt verboten (vermeidet Thread-Pool-Starvation und Deadlocks).

## 6. Korrekte DI-Lifetimes
Nutze `Scoped` für Datenbank-Kontexte (DbContext) und Unit-of-Work. `Transient` für zustandslose, leichte Services. `Singleton` nur für wirklich zustandslose, globale Caches oder Konfigurationen.

## 7. Captive Dependencies vermeiden
Injecte *niemals* einen `Scoped`-Service in einen `Singleton`-Service.

## 8. N+1-Problem eliminieren
Verwende explizites `.Include()` / `.ThenInclude()` oder `.AsSplitQuery()` für verwandte Daten.

## 9. Read-Only-Optimierung
Nutze `.AsNoTracking()` für alle Abfragen, die keine Entitäten aktualisieren.

## 10. Server-Side Evaluation sicherstellen
Stelle sicher, dass LINQ-Abfragen vollständig in SQL übersetzt werden können. Vermeide Client-Side-Evaluation (kein Aufruf von C#-Methoden innerhalb von `.Where()` oder `.Select()`, die nicht von Npgsql übersetzt werden).

## 11. Projektion auf DTOs
Nutze `.Select()`, um nur die benötigten Spalten abzufragen, anstatt ganze Entitäts-Objekte zu materialisieren.

## 12. Npgsql-Features nutzen
Verwende datenbankspezifische Optimierungen, wo sinnvoll (z.B. Mapping auf `jsonb` für flexible Daten, effiziente Index-Nutzung).

## 13. Connection Pooling respektieren
Öffne und schließe Verbindungen schnell. Verlasse dich auf das integrierte Npgsql-Pooling. Halte Transaktionen so kurz wie möglich.

## 14. Immutabilität bevorzugen
Verwende `record`-Typen für DTOs, Queries und Ergebnisse.

## 15. Moderne C# Syntax (.NET 10)
Nutze Primary Constructors, Pattern Matching und Collection Expressions, um Code prägnant und lesbar zu halten.

## 16. Strikte Nullability
Aktiviere und beachte strikt `<Nullable>enable</Nullable>`. Vermeide `null` wo möglich, nutze leere Collections oder Option-Typen.

## 17. Don't Repeat Yourself (DRY)
Extrahiere wiederholte Logik in wiederverwendbare, gut benannte Methoden oder Services.

## 18. Keep It Simple, Stupid (KISS)
Bevorzuge einfache, lesbare Lösungen gegenüber übermäßig cleveren oder stark abstrahierten Konstrukten.

## 19. You Aren't Gonna Need It (YAGNI)
Implementiere keine Funktionalität oder Abstraktionen "für die Zukunft", die nicht aktuell benötigt wird.

## 20. Fehlerbehandlung & Resilienz
Fange nur spezifische Exceptions ab, die du behandeln kannst. Nutze Middleware (z.B. `IExceptionHandler` in .NET 8+) für zentralisiertes Error-Response-Formatting (Problem Details RFC 7807). Verwende Polly für Retries und Circuit Breaker.

## 21. Strukturiertes Logging
Verwende `ILogger` mit strukturierten Parametern (z.B. `LogInformation("User {UserId} created", userId)`). Keine String-Verkettung im Log. Protokolliere niemals Passwörter, Tokens oder personenbezogene Daten (PII).

## 22. Testbarkeit
Mocking-freundlich durch Interface-Injection. Für Datenbank-Logik müssen Testcontainers (PostgreSQL) verwendet werden.

## 23. Dünne Präsentationsschicht
Controller, Minimal API Endpoints und Razor Pages/Blazor-Komponenten dürfen *niemals* Business-Logik enthalten. Delegiere immer an die Application-Layer (CQRS/MediatR Use Cases).

## 24. CQRS & Application Layer
Trenne strikt zwischen Commands (Daten ändern) und Queries (Daten lesen). Queries dürfen direkt auf DTOs mappen (ohne EF Core Tracking), Commands verarbeiten Domänen-Entitäten.

## 25. Minimal APIs Struktur & Native OpenAPI
Strukturiere Minimal APIs logisch über `MapGroup()`. Nutze die in .NET 9/10 integrierte Native OpenAPI-Unterstützung (`Microsoft.AspNetCore.OpenApi`). Nutze Endpoint-Filter für kontextspezifische übergreifende Aufgaben.

## 26. Zentrale & Unified Validation
Implementiere Validierungsregeln ausschließlich mit `FluentValidation`. Integriere Validatoren zentral in der Pipeline (z.B. via MediatR Pipeline Behavior oder Custom Endpoint Filter/Middleware).

## 27. Duale Authentifizierung & Policy-basierte Autorisierung
Konfiguriere das System für JWT Bearer (APIs/Apps) und Cookie-Authentication (Razor/Blazor). Definiere Autorisierungsregeln als Policies und wende sie auf Controller, Endpoints und Blazor-Routen an.

## 28. API Versioning & Content Negotiation
Implementiere von Tag 1 an API-Versionierung (z.B. via `Asp.Versioning`). Trenne API-spezifische Endpunkte (`/api/...`) explizit von Web-spezifischen Endpunkten (`/web/...`).

## 29. Blazor-Readiness
Schreibe Blazor-Komponenten zustandslos. Lagere UI-State in dedizierte Scoped Services oder URL-Parameter aus. Nutze Code-Behind-Dateien (`.razor.cs` / `.cshtml.cs`) und binde diese an ViewModels oder Use Cases.

## 30 SPA Applikation
Die Webanwendung verhält sich wie eine Desktop-Anwendung. Round-Trips sind u vermeiden. Das UI läuft optimiert auf verschiedenen Anzeiggeräten wie Mobile, Browser und Tablet.

## 31. ASP.NET Middleware-Pipeline & Performance
Halte die Middleware-Reihenfolge ein (ExceptionHandling -> HSTS -> HTTPS Redirection -> RateLimiting -> Routing -> CORS -> Auth -> Endpoints). Nutze Response Compression (Brotli/GZip) und Response Caching.

## 32. Web-Security (CORS, CSP, Rate Limiting)
Konfiguriere CORS restriktiv nur für Minimal API Pfade. Nutze den eingebauten .NET Rate Limiter. Implementiere strenge Content Security Policy (CSP) Header für Razor/Blazor-Outputs.

## 33. Die SPOT-Datei (Single Point of Truth) etablieren
Erstelle und pflege eine Datei namens `schema.yaml` (oder `db-schema.md`) im Wurzelverzeichnis. Die Datei muss klar strukturiert, kommentiert und ohne kryptische Abkürzungen geschrieben sein.

## 34. Der "Schema-First" Workflow
Bevor *irgendeine* Änderung an der Datenbank, an EF-Core-Entities oder an Migrationen vorgenommen wird, **muss** zwingend zuerst die `schema.yaml` aktualisiert werden. Ablauf: 1) Analyse & Update der `schema.yaml`, 2) Anpassung der C#-Entitäten und EF-Core-Mappings, 3) Generierung der EF-Core-Migrationen, 4) Anpassung der abhängigen DTOs und UI-Komponenten.

## 35. Inhalt und Struktur der SPOT-Datei
Definiere für jede Entität/Tabelle: Tabellenname und Beschreibung, Spaltennamen, C#-Datentyp und korrespondierender PostgreSQL-Datentyp, Constraints (PK, FK mit Referenz und Löschregel, UNIQUE, NOT NULL), Indizes (inkl. zusammengesetzter Indizes), Default-Werte und spezielle PG-Features (z.B. `jsonb`, `uuid-ossp`).

## 36. ORM-Mapping (EF Core) strikt aus der SPOT ableiten
EF-Core-Mappings (`IEntityTypeConfiguration<T>`) dürfen nur geschrieben oder geändert werden, wenn dies direkt aus der Definition in der `schema.yaml` abgeleitet ist. Konfiguriere Tabellen- und Spaltennamen explizit so, wie sie in der SPOT-Datei stehen. Keine versteckten Konventionen.

## 37. Konsistenz über alle Schichten
Wenn die `schema.yaml` geändert wird, müssen alle abhängigen C#-Records (DTOs, Commands, Queries) und UI-Formulare konsistent angepasst werden. Es ist streng verboten, Felder in DTOs oder im UI zu definieren, die nicht in der `schema.yaml` existieren.

## 38. Kontext-übergabe bei Modell-Wechsel oder Pause
Die `schema.yaml` dient als primärer Kontext für neue KI-Agenten oder Entwickler. Führe am Ende der `schema.yaml` einen kurzen, menschenlesbaren Changelog, der dokumentiert, *warum* und *wann* das Schema zuletzt geändert wurde.

## WICHTIGE ANWEISUNG FÜR DEN KI-AGENTEN (SYSTEM PROMPT ERGÄNZUNG)
Wenn du Code generierst, überprüfe ihn stumm gegen diese 38 Regeln. Wenn ein generierter Vorschlag gegen eine Regel verstößt, verwirf ihn und generiere eine konforme Alternative. Kommentiere deine Architekturentscheidungen nur, wenn sie von diesen Standardregeln abweichen.

Bei jeder Datenbank- oder Modell-Änderung:
1. **STOPP**: Fasse keinen C#-Code und keine Datenbank an, bevor du nicht die `schema.yaml` gelesen und aktualisiert hast.
2. **UPDATE**: Nimm die Änderung in der `schema.yaml` vor. Füge bei großen Änderungen einen kurzen Kommentar im Changelog-Bereich der Datei hinzu.
3. **SYNC**: Leite aus der aktualisierten `schema.yaml` die Änderungen für C#-Entities, EF-Core Fluent API und DTOs ab.
4. **MIGRATE**: Erstelle erst im letzten Schritt die EF-Core-Migration für PostgreSQL.

Beginne deine Antwort bei Schema-Änderungen **immer** mit dem aktualisierten Ausschnitt der `schema.yaml`, bevor du den dazugehörigen C#-Code generierst.

Bei ASP.NET Core Code musst du stets das Multi-Target-Szenario (Minimal API für Apps, Razor für Web, zukünftig Blazor) berücksichtigen:
1. Erzeuge **niemals** Business-Logik in Endpoints, Controllern oder Views. Delegiere immer an die Application-Layer.
2. Wenn du Validierung implementierst, nutze FluentValidation und integriere es zentral.
3. Wenn du Sicherheitsregeln (AuthZ) schreibst, definiere sie als wiederverwendbare Policies.
4. Trenne API-spezifische Konfiguration (JWT, CORS, Native OpenAPI) sauber von Web-spezifischer Konfiguration (Cookies, CSP, Razor Options) in der `Program.cs`, idealerweise durch Auslagerung in Extension-Methods.
