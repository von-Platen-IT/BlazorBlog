namespace AspBaseProj.Application.Contracts.Bookmarks;

public record BookmarkToggleResponse(bool IsBookmarked);

public record BookmarkStatusResponse(bool IsBookmarked);

public record BookmarkedPostDto(Guid Id, string Title, string Content, string AuthorName, bool IsPublished, DateTime? PublishedAt, int LikeCount, int DislikeCount, int CommentCount);

public record BookmarkedPostsResponse(List<BookmarkedPostDto> Posts, int Total, int Page, int PageSize);