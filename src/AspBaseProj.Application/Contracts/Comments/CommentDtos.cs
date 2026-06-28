namespace AspBaseProj.Application.Contracts.Comments;

public record UserRefDto(Guid Id, string UserName);

public record CommentDto(Guid Id, string Content, Guid PostId, Guid? UserId, Guid? ParentCommentId, string? GuestName, bool IsApproved, DateTime CreatedAt, UserRefDto? User, List<CommentDto> Replies);