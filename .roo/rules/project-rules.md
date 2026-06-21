# Project Development Rules

> These rules guide ZooCode's behavior when working on this project.

---

## General Project Rules

1. Use .NET 10 and ASP.NET Core with EF Core and the Npgsql PostgreSQL provider.

2. PostgreSQL connection string is configured in appsettings.json under ConnectionStrings:DefaultConnection. Use environment variables or user secrets for non-local deployments.

3. Database auto-migrates on application startup using EF Core migrations.

4. Root superuser is seeded on first launch from appsettings.json Blog section (username: root, password: Root#12345!). Root is not assigned to any Group.

5. Role-based access control: Author (create/edit/delete own posts, comment), Admin (manage all posts, moderate comments), Viewer (read posts, comment), Public (read posts only). Root has unrestricted permissions across all features.

6. Authors can only create, edit, and delete their own blog posts. Admin and root can edit or delete any post.

7. Only root can manage user group assignments and system settings. Admin manages posts and comments only.

8. Authenticated users' comments are immediately visible (IsApproved=true). Guest comments require manual approval by Admin or root (IsApproved=false until approved).

9. Comments support nesting via ParentCommentId. There is no hard depth limit specified, but implement reasonable depth handling in the UI.

10. Blog post content is stored as markdown. The editor provides basic text layout functions.

11. Images uploaded in posts are stored as binary data (byte[]) in the Media table in the database, not on the filesystem.

12. Social media and video platform links in posts must be rendered as visually appealing embedded cards in the UI.

13. The frontend is a SPA with client-side routing — no full page round-trips. All data operations go through the Web API.

14. The UI must be fully responsive: desktop, tablet, and smartphone. All functions must be equally usable on every device class.

15. The Web API exposes all UI functions available to Author and Viewer roles. Use modern security mechanisms (JWT or equivalent).

16. Swagger/OpenAPI documentation is configured and accessible at /swagger.

17. PostgreSQL runs in Docker: image postgres:17, host port 5433 mapped to container 5432, named volume blasor_blog_data, container name aspbaseporj_db.

18. Use Fluent API for EF Core entity configuration and relationship mapping.

19. Implement proper indexes on frequently queried columns: UserName (unique), Post.CreatedAt, Post.AuthorId, Comment(PostId, IsApproved), Comment.IsApproved, Comment.ParentCommentId, Media.PostId, SystemSetting.Key (unique).

20. All API endpoints must enforce the same role-based authorization as the UI. Never expose admin-only operations to non-admin roles.

21. Validate uploaded images for allowed content types and reasonable file size before storing in the database.

22. Use proper HTTP status codes and consistent error response format across all API endpoints.

23. The project structure follows: src/AspBaseProj.Presentation as the main startup project.
