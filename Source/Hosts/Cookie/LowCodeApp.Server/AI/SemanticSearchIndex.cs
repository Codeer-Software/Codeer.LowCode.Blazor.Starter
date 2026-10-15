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

        public static SemanticSearchService Service { get; } = new(() => _provider.Value, () => DesignerService.GetDesignData())
        {
            //SourceFields に書いた FileField の添付の中身を索引に入れるときに読む置き場所 (外すとファイル名だけ)
            FileStorages = () => FileStorageTable.Storages,
        };
    }
}
