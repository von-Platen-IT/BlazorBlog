namespace AspBaseProj.Application.Contracts.Ratings;

public record RatingResponse(int LikeCount, int DislikeCount, string? UserRating);