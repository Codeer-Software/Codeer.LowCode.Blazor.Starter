using Codeer.LowCode.Blazor.Extras.Server.AI.Embedding;
using Codeer.LowCode.Blazor.Extras.Server.AI.SemanticSearch;
using LowCodeApp.Server.Services;

namespace LowCodeApp.Server.AI
{
    /// <summary>
    /// SemanticSearchField (意味検索) のサーバー側入口。埋め込みプロバイダは appsettings の SemanticSearch.EmbeddingProvider で選ぶ。
    /// </summary>
    internal static class SemanticSearchIndex
    {
        static readonly Lazy<IEmbeddingProvider?> _provider = new(() => EmbeddingProviderTable.Create(SystemConfig.Instance.SemanticSearch.EmbeddingProvider));

        public static SemanticSearchService Service { get; } = new(() => _provider.Value, () => DesignerService.GetDesignData());
    }
}
