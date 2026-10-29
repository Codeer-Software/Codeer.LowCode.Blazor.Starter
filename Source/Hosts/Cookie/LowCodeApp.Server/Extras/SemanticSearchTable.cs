using Codeer.LowCode.Blazor.Extras.Server.AI.Embedding;
using Codeer.LowCode.Blazor.Extras.Server.AI.SemanticSearch;
using LowCodeApp.Server.Infrastructure;

namespace LowCodeApp.Server.Extras
{
    /// <summary>
    /// SemanticSearchField (意味検索) のサーバー側入口と、埋め込みプロバイダの呼び名 (appsettings の SemanticSearch.EmbeddingProvider) → 実装 (<see cref="IEmbeddingProvider"/>) の対応表。
    /// 別のプロバイダ (OpenAI / Ollama 等) を使うときは <see cref="IEmbeddingProvider"/> を実装して CreateEmbeddingProvider の switch に 1 行足す。
    /// モデルを変えたら DB のベクトル列の次元を合わせて作り直し、SemanticSearchField のスクリプト Reindex で全行を再索引する。
    /// </summary>
    public static class SemanticSearchTable
    {
        static readonly Lazy<IEmbeddingProvider?> _provider = new(() => CreateEmbeddingProvider(SystemConfig.Instance.SemanticSearch.EmbeddingProvider));

        /// <summary>プロセスに 1 つ。索引付け (CustomizedModuleDataIO の Interceptor)・再索引 (ExtrasApiTable の SemanticSearchApi)・AI チャットの search_records が使う。</summary>
        public static SemanticSearchService Service { get; } = new(() => _provider.Value, () => AppDesign.Store.GetDesignData())
        {
            //SourceFields に書いた FileField の添付の中身を索引に入れるときに読む置き場所 (外すとファイル名だけ)
            FileStorages = () => FileStorageTable.Storages,
        };

        public static IEmbeddingProvider? CreateEmbeddingProvider(string name)
        {
            var config = SystemConfig.Instance;
            return name switch
            {
                "AzureOpenAI" => new AzureOpenAIEmbeddingProvider(config.AzureOpenAIEmbedding),
                _ => null,
            };
        }
    }
}
