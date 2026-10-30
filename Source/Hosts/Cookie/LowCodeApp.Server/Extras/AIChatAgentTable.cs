using Codeer.LowCode.Blazor.DbAccess;
using Codeer.LowCode.Blazor.DesignLogic;
using Codeer.LowCode.Blazor.Extras.Server.AI;
using Codeer.LowCode.Blazor.Extras.Server.AI.Chat;
using Codeer.LowCode.Blazor.Extras.Server.AI.Chat.ModuleDataAccess;
using Codeer.LowCode.Blazor.Extras.Server.AI.Chat.RawDataAccess;
using LowCodeApp.Server.Infrastructure;
using System.Collections.Concurrent;

namespace LowCodeApp.Server.Extras
{
    /// <summary>
    /// AIChatField の Agent 名 → Agent の対応表。独自の Agent (<see cref="IAIChatAgent"/> 実装) を足すときは switch に 1 行足す。
    /// "" / "RawDataAccess" = RawDataAccessAgent (アプリの DB を読んで答える。AISettings が設定されているときだけ使える)
    /// "ModuleDataAccess" = ModuleDataAccessAgent (ログインユーザーの権限でレコードを読んで答える。行の条件・項目の読み取り権限が画面と同じに効く)
    /// </summary>
    public static class AIChatAgentTable
    {
        static readonly ConcurrentDictionary<string, Lazy<IAIChatAgent?>> _agents = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>AI チャットの入口 (Extras/ExtrasApiTable.cs が使う)。</summary>
        public static AIChatService Service { get; } = new(Create);

        /// <summary>名前に対応する Agent (無ければ null = ジョブは error)。</summary>
        public static IAIChatAgent? Create(string name)
            => _agents.GetOrAdd(name ?? string.Empty, n => new Lazy<IAIChatAgent?>(() => CreateCore(n))).Value;

        static IAIChatAgent? CreateCore(string name) => name switch
        {
            "" => CreateRawDataAccess(),
            "RawDataAccess" => CreateRawDataAccess(),
            "ModuleDataAccess" => CreateModuleDataAccess(),
            _ => null,
        };

        //読むデータソースは appsettings の AIChat:RawDataAccessDataSources (空なら DataSources の全部)
        static IAIChatAgent? CreateRawDataAccess()
        {
            var config = SystemConfig.Instance;
            //別のプロバイダ (OpenAI / Ollama 等) を使うなら IChatClient をここで作って渡す
            var chatClientFactory = AzureOpenAIClients.ChatClientFactory(config.AISettings);
            if (chatClientFactory == null) return null;
            var options = config.AIChat.RawDataAccess;
            options.DataSourceNames = config.AIChat.RawDataAccessDataSources.Length == 0
                ? config.DataSources.Select(e => e.Name).ToList()
                : config.AIChat.RawDataAccessDataSources.ToList();
            return new RawDataAccessAgent(
                chatClientFactory,
                () => new DbAccessor(config.DataSources),
                () => AppDesign.Store.GetDesignData(),
                folder => DesignDataFileManager.GetResourceTexts(config.DesignFileDirectory, folder, ".md", ".txt").Select(e => new AIChatDocument(e.Name, e.Text)).ToList(),
                options,
                semanticSearch: SemanticSearchTable.Service);
        }

        //レコードは依頼したユーザーの権限で読む。上限は appsettings の AIChat:ModuleDataAccess
        static IAIChatAgent? CreateModuleDataAccess()
        {
            var config = SystemConfig.Instance;
            var chatClientFactory = AzureOpenAIClients.ChatClientFactory(config.AISettings);
            if (chatClientFactory == null) return null;
            return new ModuleDataAccessAgent(
                chatClientFactory,
                userId =>
                {
                    var dataService = new DataService(userId);
                    return Task.FromResult(new ModuleDataAccessScope(dataService.ModuleDataIO, dataService, dataService.DbAccess));
                },
                () => AppDesign.Store.GetDesignData(),
                folder => DesignDataFileManager.GetResourceTexts(config.DesignFileDirectory, folder, ".md", ".txt").Select(e => new AIChatDocument(e.Name, e.Text)).ToList(),
                config.AIChat.ModuleDataAccess,
                //SemanticSearchField を置いたモジュールを意味検索 (search_records) で探せるようにする
                semanticSearch: SemanticSearchTable.Service);
        }
    }
}
