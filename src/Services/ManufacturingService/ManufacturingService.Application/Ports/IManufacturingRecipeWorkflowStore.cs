using His.Hope.Contracts.Manufacturing;

namespace His.Hope.ManufacturingService.Application.Ports;

public interface IManufacturingRecipeWorkflowStore
{
    RecipeDto CreateRecipe(CreateRecipeRequest request);
    IReadOnlyList<RecipeDto> GetRecipes(string? tenantKey, string? productSku, bool? active, int limit);
    (RecipeDto? Recipe, string? Error) ChangeRecipeLifecycle(Guid recipeId, string tenantKey, string targetStatus, RecipeLifecycleRequest request);
    (LossReviewDto? Review, string? Error) ReviewLoss(string tenantKey, Guid batchId, Guid operationId, LossReviewRequest request);
}
