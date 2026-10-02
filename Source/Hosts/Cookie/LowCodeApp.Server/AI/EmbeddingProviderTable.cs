using Codeer.LowCode.Blazor.Extras.Server.AI.Embedding;
using LowCodeApp.Server.Services;

namespace LowCodeApp.Server.AI
{
    /// <summary>
    /// 埋め込みプロバイダの呼び名 (appsettings の SemanticSearch.EmbeddingProvider) → 実装 (<see cref="IEmbeddingProvider"/>) の対応表。
    /// 別のプロバイダ (OpenAI / Ollama 等) を使うときは <see cref="IEmbeddingProvider"/> を実装してこの switch に 1 行足す。
    /// モデルを変えたら DB のベクトル列の次元を合わせて作り直し、SemanticSearchField のスクリプト Reindex で全行を再索引する。
    /// </summary>
    public static class EmbeddingProviderTable
    {
        public static IEmbeddingProvider? Create(string name)
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
