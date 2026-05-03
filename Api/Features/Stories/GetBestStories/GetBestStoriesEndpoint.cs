using Carter;

namespace Api.Features.Stories.GetBestStories;

public class GetBestStoriesEndpoint : ICarterModule
{
    private const int MaxStories = 500;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/stories/best/{total}", async (int total, IGetBestStoriesUseCase useCase, CancellationToken cancellationToken) =>
        {
            if (total <= 0 || total > MaxStories)
                return Results.Problem(
                    title: "Invalid request",
                    detail: $"'total' must be between 1 and {MaxStories}.",
                    statusCode: StatusCodes.Status400BadRequest);

            var response = await useCase.HandleAsync(total, cancellationToken);

            return Results.Ok(response);
        })
        .WithName("GetBestStories")
        .WithTags("Stories");
    }
}
