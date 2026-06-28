namespace AspBaseProj.Application.Contracts.Posts;

public record AuthorDto(Guid Id, string UserName);

public record PostDto(Guid Id, string Title, string Content, Guid AuthorId, bool IsPublished, DateTime CreatedAt, DateTime? PublishedAt, AuthorDto? Author, int LikeCount = 0, int DislikeCount = 0);

public record PostListResponse(List<PostDto> Posts, int Total, int Page, int PageSize);

public record MyPostDto(Guid Id, string Title, string Content, bool IsPublished, DateTime CreatedAt, DateTime? UpdatedAt, DateTime? PublishedAt, int CommentCount);

public record MyPostsResponse(List<MyPostDto> Posts, int Total, int Page, int PageSize);