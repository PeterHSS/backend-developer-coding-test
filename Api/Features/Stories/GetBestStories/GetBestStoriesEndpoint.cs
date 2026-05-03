using Carter;

namespace Api.Features.Stories.GetBestStories;

public class GetBestStoriesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/stories/best/{total}", async (int total, IGetBestStoriesUseCase useCase, CancellationToken cancellationToken) =>
        {
            var response = await useCase.HandleAsync(total, cancellationToken);

            return Results.Ok(response);
        })
        .WithName("GetBestStories")
        .WithTags("Stories");
    }
}
