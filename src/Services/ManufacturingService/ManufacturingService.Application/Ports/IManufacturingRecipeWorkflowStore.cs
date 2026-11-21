using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingRecipeWorkflowStore
{
    RecipeDto CreateRecipe(CreateRecipeRequest request);
    IReadOnlyList<RecipeDto> GetRecipes(string? productSku, bool? active, int limit, int page = 1);
    // Compatibility seam for callers that still pass a tenant selector.
    IReadOnlyList<RecipeDto> GetRecipes(string? tenantKey, string? productSku, bool? active, int limit, int page = 1);
    (RecipeDto? Recipe, string? Error) ChangeRecipeLifecycle(Guid recipeId, string tenantKey, string targetStatus, RecipeLifecycleRequest request);
    Task<(LossReviewDto? Review, string? Error)> ReviewLossAsync(string tenantKey, Guid batchId, Guid operationId, LossReviewRequest request, CancellationToken cancellationToken = default);
}
