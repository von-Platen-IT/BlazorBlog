# AGENTS.md — BlazorBlog

This file provides guidance to AI agents when working with code in this repository.

**Technology:** .NET 10 Fullstack (ASP.NET Core, PostgreSQL, EF Core)
**Project:** BlazorBlog

---

## Key Configuration Files

| File | Purpose |
|------|---------|
| `.roomodes` | Custom ZooCode modes for this project |
| `.roo/rules/database-schema.md` | Database schema (single source of truth) |
| `.roo/rules/ui-specification.md` | UI specification (single source of truth) |
| `.roo/rules/project-rules.md` | Development rules and guidelines |

## Architecture

**Project Summary:** A multi-user blog platform built with ASP.NET Core .NET 10 and PostgreSQL. It features role-based access control (Author, Admin, Viewer, Public, and a root superuser), a rich-text/markdown editor for authors with image upload and social media embedding, nested comments with guest moderation workflow, a responsive SPA frontend, and a Web API with Swagger documentation.

### Database

- The database schema is defined in `.roo/rules/database-schema.md`
- This is the **single source of truth** for all data models
- When modifying the schema, edit the file FIRST, then update code accordingly

### User Interface

- The UI specification is defined in `.roo/rules/ui-specification.md`
- This is the **single source of truth** for all UI components and pages
- When modifying the UI, edit the file FIRST, then update code accordingly

## Development Workflow

1. Schema/UI changes: Update the respective `.roo/rules/` file first
2. ZooCode will use these files as reference when generating or modifying code
3. Always maintain consistency between the specification files and actual implementation

## Detected Frameworks & Libraries

- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- Docker
- Swagger/OpenAPI
- SPA (Single Page Application)
