# Comment System Redesign — Planungsdokument

> **Status:** ENTWURF — wartet auf Abnahme  
> **Erstellt:** 2026-06-25  
> **Ziel:** Vollständiges Neu-Design des Kommentarsystems als abgegrenztes Modul mit AJAX-artiger Sofortreaktion, visueller Verschachtelung und professioneller OOP-API-Nutzung

---

## 1. Ist-Zustand & Probleme

### Aktuelle Architektur

```
PostDetail.razor
  ├── CommentView.razor (rekursiv, gibt Reply-Events an Parent)
  │     └── CommentView.razor (rekursiv für Replies)
  └── CommentForm (inline in PostDetail)
```

**Probleme:**

| Problem | Beschreibung |
|---------|-------------|
| **Kein Sofort-Feedback** | Nach Submit wird `comments = await Api.GetCommentsAsync(Id)` aufgerufen — komplette Neu-Ladung ALLER Kommentare |
| **Event-Bubbling** | `CommentView` feuert `OnReplyAdded`-Event an `PostDetail`, das dann `Api.ReplyToCommentAsync` aufruft und alle Kommentare neu lädt |
| **Keine visuelle Hierarchie** | Nur `ms-4` Einrückung, keine Linien/Verbinder zwischen Parent und Reply |
| **Gast-Kommentare verschwinden** | Gast-Kommentar wird gesendet, aber statt Anzeige erscheint nur "Vielen Dank"-Meldung |
| **Kein Modul** | Kommentar-Logik ist über `PostDetail` und `CommentView` verstreut, keine klare API-Grenze |
| **Round-Trip bei jeder Aktion** | Jede Kommentar-Aktion (add, reply) lädt den gesamten Kommentar-Baum neu |

---

## 2. Ziel-Architektur

### Modulares Design

```
PostDetail.razor
  └── <CommentSection PostId="Id" />   ← Eine Komponente, gekapselt

CommentSection.razor  (NEU — Modul-Wurzel)
  ├── CommentNode.razor (NEU — rekursiv, autark)
  │     ├── ReplyForm (inline, kein Event-Bubbling)
  │     └── CommentNode.razor (rekursiv für Replies)
  └── NewCommentForm (Top-Level-Formular)
```

### OOP-Prinzipien

- **Single Responsibility:** `CommentSection` managed nur den Kommentar-Baum. `CommentNode` managed nur einen einzelnen Kommentar + seine Replies.
- **Dependency Inversion:** Beide Komponenten injizieren `ApiClient` direkt, kein Event-Bubbling zum Parent.
- **Encapsulation:** Der gesamte State (Kommentare, Reply-Forms, Loading-States) lebt im Modul.
- **Interface-Segregation:** `ApiClient`-Methoden sind auf das Nötigste beschränkt.

---

## 3. API-Design (Unverändert — Wiederverwendung)

Die bestehenden Endpoints werden unverändert verwendet:

| Methode | Route | Beschreibung |
|---------|-------|-------------|
| `GET` | `/api/comments/post/{postId}` | Alle approved Top-Level-Kommentare + Replies laden |
| `POST` | `/api/comments/post/{postId}` | Neuen Top-Level-Kommentar erstellen |
| `POST` | `/api/comments/{id}/reply` | Reply zu einem Kommentar erstellen |

**Wichtig:** Die API-Antworten liefern das erstellte `CommentDto`-Objekt zurück (inkl. `Id`, `CreatedAt`, etc.), sodass die UI den neuen Kommentar sofort in den lokalen State einfügen kann — ohne kompletten Neu-Load.

---

## 4. Komponenten-Design

### 4.1 `CommentSection.razor` — Modul-Wurzel

**Route:** Keine (wird als Child-Komponente verwendet)  
**Parameter:** `PostId` (Guid)

```
┌─────────────────────────────────────────────────────────┐
│  💬 Comments (5)                                        │
│                                                         │
│  ┌─ NewCommentForm ───────────────────────────────────┐ │
│  │ [Guest Name]  [Guest Email]   (wenn nicht auth)    │ │
│  │ [_____________________________________________]    │ │
│  │ [Submit]                                           │ │
│  └────────────────────────────────────────────────────┘ │
│                                                         │
│  ┌─ CommentNode ──────────────────────────────────────┐ │
│  │ 👍 AuthorName • 2 min ago                          │ │
│  │ This is a great post!                              │ │
│  │ [Reply]                                            │ │
│  │ ┌─ CommentNode (nested) ─────────────────────────┐ │ │
│  │ │ 👤 GuestName • 1 min ago                      │ │ │
│  │ │ Thanks for the info!                           │ │ │
│  │ │ [Reply]                                        │ │ │
│  │ └───────────────────────────────────────────────┘ │ │
│  └─────────────────────────────────────────────────────┘ │
│                                                         │
│  ┌─ CommentNode ──────────────────────────────────────┐ │
│  │ ...                                                │ │
│  └─────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

**State:**
```csharp
private List<CommentDto> comments = new();     // Top-Level-Kommentare
private bool isLoading;
private string? error;
```

**Methoden:**
```csharp
Task LoadCommentsAsync()              // GET /api/comments/post/{postId}
Task AddTopLevelCommentAsync(...)     // POST → fügt neuen CommentDto in comments ein
```

### 4.2 `CommentNode.razor` — Einzelner Kommentar (rekursiv)

**Parameter:**
- `CommentDto Comment` — Der anzuzeigende Kommentar
- `int Depth` — Verschachtelungstiefe (0 = Top-Level)
- `List<CommentDto> Siblings` — Referenz auf die Geschwister-Liste (für Einfügen von Replies)

```
┌─ Tree-Line Container ──────────────────────────────────┐
│  ┌─ Depth 0: keine linke Linie                        │
│  │ ┌─ Depth 1: dünne graue Linie links ─────────────┐ │
│  │ │ ┌─ Depth 2: weitere Linie ────────────────────┐ │ │
│  │ │ │  Author • Time                               │ │ │
│  │ │ │  Content                                     │ │ │
│  │ │ │  [Reply]                                     │ │ │
│  │ │ │  (ReplyForm inline)                          │ │ │
│  │ │ └─────────────────────────────────────────────┘ │ │
│  │ │  ┌─ Replies (rekursiv) ───────────────────────┐ │ │
│  │ │  │  CommentNode...                            │ │ │
│  │ │  └────────────────────────────────────────────┘ │ │
│  │ └───────────────────────────────────────────────┘ │
│  └───────────────────────────────────────────────────┘
└─────────────────────────────────────────────────────────┘
```

**Visuelle Hierarchie (CSS):**

```css
.comment-thread {
    position: relative;
    padding-left: 24px;
}

.comment-thread::before {
    content: '';
    position: absolute;
    left: 8px;
    top: 0;
    bottom: 0;
    width: 2px;
    background: #dee2e6;       /* graue vertikale Linie */
}

/* Letztes Kind: Linie nur bis zur Mitte */
.comment-thread:last-child::before {
    height: 50%;
}

.comment-node {
    position: relative;
    padding: 12px;
    border: 1px solid #e9ecef;
    border-radius: 8px;
    background: #fff;
    margin-bottom: 8px;
}

/* Horizontale Verbindungslinie zum Parent */
.comment-node::before {
    content: '';
    position: absolute;
    left: -16px;
    top: 24px;
    width: 16px;
    height: 2px;
    background: #dee2e6;
}
```

**State:**
```csharp
private bool showReplyForm;
private string replyText = "";
private string replyGuestName = "";
private string replyGuestEmail = "";
private bool isSubmitting;
```

**Methoden:**
```csharp
async Task SubmitReplyAsync()    // POST /api/comments/{id}/reply → fügt neuen CommentDto in Comment.Replies ein
```

### 4.3 Kein eigenes `NewCommentForm` — Inline in `CommentSection`

Das Top-Level-Formular bleibt in `CommentSection` (spart eine Datei, ist überschaubar).

---

## 5. Datenfluss (Optimistic Update)

### Top-Level-Kommentar hinzufügen

```
User klickt "Submit"
  → CommentSection.AddTopLevelCommentAsync()
    → Api.AddCommentAsync(postId, content, guestName, guestEmail)
    → Server antwortet mit CommentDto { Id, Content, CreatedAt, User, IsApproved, ... }
    → Füge CommentDto in comments-Liste ein (am Anfang)
    → Blazor re-rendert automatisch → Kommentar erscheint sofort
    → Formular wird zurückgesetzt (Text = "")
```

### Reply hinzufügen

```
User klickt "Reply" in CommentNode
  → Reply-Formular erscheint inline
  → User klickt "Submit Reply"
  → CommentNode.SubmitReplyAsync()
    → Api.ReplyToCommentAsync(commentId, text, guestName, guestEmail)
    → Server antwortet mit CommentDto (der neue Reply)
    → Füge CommentDto in Comment.Replies-Liste ein
    → Blazor re-rendert automatisch → Reply erscheint sofort
    → Formular wird geschlossen
```

**Kein `GET /api/comments/post/{postId}`** nach dem Hinzufügen — die API-Antwort des POST/CREATE enthält bereits alle nötigen Daten.

---

## 6. Gast-Kommentare: Sofortige Anzeige

**Aktuell:** Gast-Kommentare werden gesendet, dann verschwindet das Formular und "Vielen Dank" erscheint. Der Gast sieht seinen Kommentar nicht.

**Neu:** Gast-Kommentare werden sofort im Baum angezeigt, aber mit einem `⚠️ Pending Approval`-Badge. Der `IsApproved`-Status aus der API-Antwort (`false` für Gäste) steuert die Anzeige:

```razor
@if (!Comment.IsApproved)
{
    <span class="badge bg-warning text-dark">⏳ Pending Approval</span>
}
```

Der Gast sieht seinen Kommentar sofort — mit Hinweis, dass er noch freigeschaltet wird.

---

## 7. Datei-Änderungen

### Neue Dateien (2)

| Datei | Beschreibung |
|-------|-------------|
| `CommentSection.razor` | Modul-Wurzel: Lädt Kommentare, Top-Level-Formular, rendert `CommentNode`-Liste |
| `CommentNode.razor` | Einzelner Kommentar mit Reply-Formular, rekursiv für Replies, Tree-Lines |

### Zu ändernde Dateien (3)

| Datei | Änderung |
|-------|----------|
| [`PostDetail.razor`](src/AspBaseProj.Presentation/Components/Pages/PostDetail.razor) | Kommentar-Bereich ersetzen durch `<CommentSection PostId="Id" />` |
| [`CommentView.razor`](src/AspBaseProj.Presentation/Components/Shared/CommentView.razor) | **LÖSCHEN** — wird durch `CommentNode.razor` ersetzt |
| `wwwroot/css/app.css` | Tree-Line CSS hinzufügen |

### Nicht geändert

- `Comment.cs` (Entity) — unverändert
- `ICommentRepository.cs` / `CommentRepository.cs` — unverändert
- `Program.cs` (API-Endpoints) — unverändert
- `ApiClient.cs` — unverändert (bestehende Methoden werden verwendet)
- `.roo/rules/database-schema.md` — unverändert
- `.roo/rules/ui-specification.md` — wird aktualisiert

---

## 8. Implementierungs-Phasen

### Phase 1: CommentNode.razor (NEU)
- Rendert einen Kommentar mit Autor, Zeit, Inhalt
- Inline Reply-Formular mit Submit/Cancel
- Direkter ApiClient-Aufruf für Reply (kein Event-Bubbling)
- Optimistic Update: Reply sofort in `Comment.Replies` einfügen
- Tree-Line CSS-Klassen

### Phase 2: CommentSection.razor (NEU)
- Lädt Kommentare via `ApiClient.GetCommentsAsync(postId)`
- Top-Level-Formular für neue Kommentare
- Rendert `CommentNode` für jeden Top-Level-Kommentar
- Optimistic Update: Neuer Kommentar sofort in Liste einfügen
- Gast-Kommentare mit "Pending Approval"-Badge anzeigen

### Phase 3: PostDetail.razor (ÄNDERN)
- Alten Kommentar-Bereich + `CommentView`-Referenzen entfernen
- Einfügen: `<CommentSection PostId="Id" />`
- Entfernen von `HandleReplyAdded`, `AddComment`, Kommentar-State-Variablen

### Phase 4: CSS (ÄNDERN)
- Tree-Line Styles in `app.css`
- Responsive Anpassungen für Mobile

### Phase 5: Aufräumen
- `CommentView.razor` löschen
- UI-Spec aktualisieren

---

## 9. Zusammenfassung

| Metrik | Vorher | Nachher |
|--------|--------|---------|
| Kommentar-Komponenten | 1 (`CommentView`) | 2 (`CommentSection` + `CommentNode`) |
| Event-Bubbling | Ja (Reply → Parent) | Nein (direkter ApiClient) |
| Sofort-Feedback | Nein (vollständiger Reload) | Ja (optimistic Insert) |
| Visuelle Hierarchie | Nur Einrückung | Einrückung + Tree-Lines |
| Gast-Kommentar sichtbar | Nein ("Vielen Dank") | Ja (mit "Pending"-Badge) |
| Round-Trips pro Reply | 2 (POST + GET all) | 1 (POST only) |
| Modulare Kapselung | Nein | Ja (autarke Komponenten) |
| Neue Dateien | — | 2 |
| Gelöschte Dateien | — | 1 |
| Geänderte Dateien | — | 2 |
| API-Änderungen | — | 0 |
| DB-Änderungen | — | 0 |

---

## 10. Nächste Schritte

1. ⬜ Plan abnehmen
2. ⬜ Phase 1: `CommentNode.razor` erstellen
3. ⬜ Phase 2: `CommentSection.razor` erstellen
4. ⬜ Phase 3: `PostDetail.razor` umbauen
5. ⬜ Phase 4: CSS Tree-Lines
6. ⬜ Phase 5: Aufräumen + UI-Spec
7. ⬜ Build-Verifikation